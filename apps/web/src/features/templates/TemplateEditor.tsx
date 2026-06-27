import {
  type CSSProperties,
  type DragEvent,
  type FormEvent,
  type PointerEvent as ReactPointerEvent,
  useState,
} from 'react';
import { Icon } from '../../components/Icon';
import { fieldTypes, type EditableTemplateField } from '../../appTypes';
import { FIELD_LAYOUT_MAX_COLUMNS } from '../../templateLayout';
import {
  clampInteger,
  renumberTemplateFields,
  splitOptions,
  toEditableTemplateField,
} from '../../templateFieldUtils';
import type {
  FieldDefinitionScope,
  FieldDefinitionType,
  TaskTemplateDetailResponse,
  TaskTemplateLayoutResponse,
  TaskTemplateLayoutRow,
  UpsertFieldDefinitionRequest,
} from '../../types';

type TemplateLayoutRows = Record<FieldDefinitionScope, number[]>;
type TemplateLayoutWeights = Record<FieldDefinitionScope, number[][]>;
type TemplateLayoutHeights = Record<FieldDefinitionScope, number[]>;
type TemplateFieldModalState = {
  clientId?: string;
  column: number;
  row: number;
  scope: FieldDefinitionScope;
} | null;
type TemplateFieldDraft = {
  name: string;
  optionsText: string;
  required: boolean;
  type: FieldDefinitionType;
};
type TemplateColumnRemovalState = {
  boundaryIndex: number;
  fieldNames: string[];
  rowIndex: number;
  scope: FieldDefinitionScope;
} | null;
type TemplateRowRemovalState = {
  fieldNames: string[];
  rowIndex: number;
  scope: FieldDefinitionScope;
} | null;

const templateScopes: FieldDefinitionScope[] = ['Header', 'Entry'];
const minimumColumnWeight = 0.12;
const defaultRowHeight = 132;
const longTextRowHeight = 190;
const minRowHeight = 72;
const maxRowHeight = 420;

function getStoredLayoutRows(
  template: TaskTemplateDetailResponse | null,
  scope: FieldDefinitionScope,
) {
  return scope === 'Header'
    ? template?.layout?.header ?? []
    : template?.layout?.entry ?? [];
}

function createTemplateLayoutRows(
  fields: EditableTemplateField[],
  template: TaskTemplateDetailResponse | null,
): TemplateLayoutRows {
  return Object.fromEntries(
    templateScopes.map((scope) => {
      const scopedFields = fields.filter((field) => field.scope === scope);
      const storedRows = getStoredLayoutRows(template, scope);
      const maxRow = Math.max(
        1,
        ...scopedFields.map((field) => clampInteger(field.layoutRow, 1, 24)),
        ...storedRows.map((row) => clampInteger(row.row, 1, 24)),
      );
      const rows = Array.from({ length: maxRow }, (_, rowIndex) => {
        const rowNumber = rowIndex + 1;
        const storedRow = storedRows.find((row) => row.row === rowNumber);
        const rowFields = scopedFields.filter(
          (field) => clampInteger(field.layoutRow, 1, 24) === rowNumber,
        );

        return Math.max(
          1,
          storedRow?.columnWeights.length ?? 0,
          ...rowFields.map((field) =>
            clampInteger(field.layoutColumn, 1, FIELD_LAYOUT_MAX_COLUMNS)),
        );
      });

      return [scope, rows];
    }),
  ) as TemplateLayoutRows;
}

function fieldOccupiesTemplateCell(
  field: EditableTemplateField,
  row: number,
  column: number,
) {
  return field.layoutRow === row && field.layoutColumn === column;
}

function normalizeFieldToLayoutRows(
  field: EditableTemplateField,
  rows: number[],
): EditableTemplateField {
  const row = clampInteger(field.layoutRow, 1, Math.max(1, rows.length));
  const columnCount = rows[row - 1] ?? 1;
  const column = clampInteger(field.layoutColumn, 1, columnCount);
  const columnSpan = 1;

  return {
    ...field,
    layoutRow: row,
    layoutColumn: column,
    layoutColumnSpan: columnSpan,
    layoutRowSpan: 1,
    layoutWeight: normalizeLayoutWeight(field.layoutWeight),
  };
}

function createColumnWeights(columnCount: number) {
  return Array.from({ length: columnCount }, () => 1);
}

function normalizeColumnWeights(weights: number[] | undefined, columnCount: number) {
  if (!weights?.length) {
    return createColumnWeights(columnCount);
  }

  if (weights.length === columnCount) {
    return weights.map((weight) => Math.max(minimumColumnWeight, weight));
  }

  if (weights.length < columnCount) {
    return [
      ...weights.map((weight) => Math.max(minimumColumnWeight, weight)),
      ...createColumnWeights(columnCount - weights.length),
    ];
  }

  return weights
    .slice(0, columnCount)
    .map((weight) => Math.max(minimumColumnWeight, weight));
}

function normalizeLayoutWeight(weight: number | undefined) {
  if (!Number.isFinite(weight)) {
    return 1;
  }

  return Math.min(12, Math.max(minimumColumnWeight, weight ?? 1));
}

function normalizeLayoutHeight(height: number | undefined, fallback = defaultRowHeight) {
  if (!Number.isFinite(height)) {
    return fallback;
  }

  return Math.min(maxRowHeight, Math.max(minRowHeight, height ?? fallback));
}

function getDefaultRowHeight(fields: EditableTemplateField[]) {
  return fields.some((field) => field.type === 'LongText')
    ? longTextRowHeight
    : defaultRowHeight;
}

function createTemplateLayoutWeights(
  fields: EditableTemplateField[],
  rows: TemplateLayoutRows,
  template: TaskTemplateDetailResponse | null,
): TemplateLayoutWeights {
  return Object.fromEntries(
    templateScopes.map((scope) => [
      scope,
      rows[scope].map((columnCount, rowIndex) => {
        const rowNumber = rowIndex + 1;
        const storedRow = getStoredLayoutRows(template, scope)
          .find((row) => row.row === rowNumber);

        if (storedRow?.columnWeights.length) {
          return normalizeColumnWeights(storedRow.columnWeights, columnCount);
        }

        return Array.from({ length: columnCount }, (_, columnIndex) => {
          const columnNumber = columnIndex + 1;
          const field = fields.find((candidate) =>
            candidate.scope === scope &&
            candidate.layoutRow === rowNumber &&
            candidate.layoutColumn === columnNumber);

          return normalizeLayoutWeight(field?.layoutWeight);
        });
      }),
    ]),
  ) as TemplateLayoutWeights;
}

function createTemplateLayoutHeights(
  fields: EditableTemplateField[],
  rows: TemplateLayoutRows,
  template: TaskTemplateDetailResponse | null,
): TemplateLayoutHeights {
  return Object.fromEntries(
    templateScopes.map((scope) => [
      scope,
      rows[scope].map((_, rowIndex) => {
        const rowNumber = rowIndex + 1;
        const storedRow = getStoredLayoutRows(template, scope)
          .find((row) => row.row === rowNumber);
        const rowFields = fields.filter((field) =>
          field.scope === scope &&
          field.layoutRow === rowNumber);

        return normalizeLayoutHeight(storedRow?.height, getDefaultRowHeight(rowFields));
      }),
    ]),
  ) as TemplateLayoutHeights;
}

function buildTemplateLayoutRequest(
  rows: TemplateLayoutRows,
  weights: TemplateLayoutWeights,
  heights: TemplateLayoutHeights,
): TaskTemplateLayoutResponse {
  const buildRows = (scope: FieldDefinitionScope): TaskTemplateLayoutRow[] =>
    rows[scope].map((columnCount, rowIndex) => ({
      row: rowIndex + 1,
      columnWeights: normalizeColumnWeights(
        weights[scope]?.[rowIndex],
        columnCount,
      ).map((weight) => Number(weight.toFixed(4))),
      height: Number(normalizeLayoutHeight(heights[scope]?.[rowIndex]).toFixed(2)),
    }));

  return {
    header: buildRows('Header'),
    entry: buildRows('Entry'),
  };
}

function getColumnBoundaryPercent(weights: number[], boundaryIndex: number) {
  const total = weights.reduce((sum, weight) => sum + weight, 0);
  const beforeBoundary = weights
    .slice(0, boundaryIndex + 1)
    .reduce((sum, weight) => sum + weight, 0);

  return total <= 0 ? 0 : (beforeBoundary / total) * 100;
}

function getColumnMidpointPercent(weights: number[], columnIndex: number) {
  const total = weights.reduce((sum, weight) => sum + weight, 0);
  const beforeColumn = weights
    .slice(0, columnIndex)
    .reduce((sum, weight) => sum + weight, 0);

  return total <= 0
    ? 0
    : ((beforeColumn + weights[columnIndex] / 2) / total) * 100;
}

function insertColumnWeightAfter(weights: number[], columnIndex: number) {
  const currentWeight = Math.max(minimumColumnWeight * 2, weights[columnIndex] ?? 1);
  const splitWeight = currentWeight / 2;

  return [
    ...weights.slice(0, columnIndex),
    splitWeight,
    splitWeight,
    ...weights.slice(columnIndex + 1),
  ];
}

export function TemplateEditor({
  onDeleteTemplate,
  onSaveTemplate,
  template,
}: {
  onDeleteTemplate: (id: string) => Promise<void>;
  onSaveTemplate: (
    id: string | null,
    name: string,
    fields: UpsertFieldDefinitionRequest[],
    layout: TaskTemplateLayoutResponse,
  ) => Promise<TaskTemplateDetailResponse | null>;
  template: TaskTemplateDetailResponse | null;
}) {
  const [name, setName] = useState(template?.name ?? '');
  const [fields, setFields] = useState<EditableTemplateField[]>(
    () => template?.fields.map(toEditableTemplateField) ?? [],
  );
  const initialFields = template?.fields.map(toEditableTemplateField) ?? [];
  const initialLayoutRows = createTemplateLayoutRows(
    initialFields,
    template,
  );
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [draggedFieldId, setDraggedFieldId] = useState<string | null>(null);
  const [activeFieldId, setActiveFieldId] = useState<string | null>(null);
  const [fieldModal, setFieldModal] = useState<TemplateFieldModalState>(null);
  const [fieldDraft, setFieldDraft] = useState<TemplateFieldDraft>({
    name: '',
    optionsText: '',
    required: false,
    type: 'Text',
  });
  const [columnRemoval, setColumnRemoval] = useState<TemplateColumnRemovalState>(null);
  const [rowRemoval, setRowRemoval] = useState<TemplateRowRemovalState>(null);
  const [layoutRows, setLayoutRows] = useState<TemplateLayoutRows>(() => initialLayoutRows);
  const [layoutWeights, setLayoutWeights] = useState<TemplateLayoutWeights>(
    () => createTemplateLayoutWeights(initialFields, initialLayoutRows, template),
  );
  const [layoutHeights, setLayoutHeights] = useState<TemplateLayoutHeights>(
    () => createTemplateLayoutHeights(initialFields, initialLayoutRows, template),
  );

  const openFieldModal = (
    scope: FieldDefinitionScope,
    row: number,
    column: number,
  ) => {
    const existingField = fields.find((field) =>
      field.scope === scope &&
      fieldOccupiesTemplateCell(field, row, column));

    if (existingField) {
      setActiveFieldId(existingField.clientId);
      setFieldDraft({
        name: existingField.name,
        optionsText: existingField.optionsText,
        required: existingField.required,
        type: existingField.type,
      });
      setFieldModal({
        clientId: existingField.clientId,
        column: existingField.layoutColumn,
        row: existingField.layoutRow,
        scope: existingField.scope,
      });
      return;
    }

    setActiveFieldId(null);
    setFieldDraft({
      name: '',
      optionsText: '',
      required: false,
      type: 'Text',
    });
    setFieldModal({ column, row, scope });
  };

  const updateField = (
    clientId: string,
    update: Partial<EditableTemplateField>,
  ) => {
    setFields((currentFields) =>
      currentFields.map((field) =>
        field.clientId === clientId
          ? normalizeFieldToLayoutRows({ ...field, ...update }, layoutRows[field.scope])
          : field,
      ),
    );
  };

  const removeLayoutColumn = (
    scope: FieldDefinitionScope,
    rowIndex: number,
    boundaryIndex: number,
    force = false,
  ) => {
    const columnCount = layoutRows[scope][rowIndex] ?? 1;

    if (columnCount <= 1) {
      return;
    }

    const rowNumber = rowIndex + 1;
    const removedColumn = boundaryIndex + 2;
    const fieldNamesInRemovedCell = fields
      .filter((field) =>
        field.scope === scope &&
        field.layoutRow === rowNumber &&
        fieldOccupiesTemplateCell(field, rowNumber, removedColumn))
      .map((field) => field.name);

    if (fieldNamesInRemovedCell.length > 0 && !force) {
      setColumnRemoval({
        boundaryIndex,
        fieldNames: fieldNamesInRemovedCell,
        rowIndex,
        scope,
      });
      return;
    }

    const currentWeights = normalizeColumnWeights(
      layoutWeights[scope][rowIndex],
      columnCount,
    );
    const mergedWeights = currentWeights
      .map((weight, index) =>
        index === boundaryIndex
          ? weight + (currentWeights[removedColumn - 1] ?? 0)
          : weight)
      .filter((_, index) => index !== removedColumn - 1);

    setLayoutRows((currentRows) => ({
      ...currentRows,
      [scope]: currentRows[scope].map((currentColumnCount, index) =>
        index === rowIndex ? Math.max(1, currentColumnCount - 1) : currentColumnCount),
    }));
      setLayoutWeights((currentWeightsByScope) => ({
        ...currentWeightsByScope,
        [scope]: currentWeightsByScope[scope].map((weights, index) =>
          index === rowIndex ? mergedWeights : weights),
      }));
    setFields((currentFields) =>
      renumberTemplateFields(
        currentFields
          .filter((field) =>
            !(field.scope === scope &&
              field.layoutRow === rowNumber &&
              fieldOccupiesTemplateCell(field, rowNumber, removedColumn)))
          .map((field) => {
            if (
              field.scope !== scope ||
              field.layoutRow !== rowNumber ||
              field.layoutColumn < removedColumn
            ) {
              return field;
            }

            return {
              ...field,
              layoutColumn: field.layoutColumn - 1,
              layoutWeight: normalizeLayoutWeight(
                mergedWeights[field.layoutColumn - 2] ?? field.layoutWeight,
              ),
            };
          }),
      ),
    );
  };

  const moveFieldToLayoutCell = (
    sourceClientId: string,
    scope: FieldDefinitionScope,
    row: number,
    column: number,
  ) => {
    setFields((currentFields) => {
      const sourceField = currentFields.find((field) => field.clientId === sourceClientId);

      if (!sourceField || sourceField.scope !== scope) {
        return currentFields;
      }

      const sourceCell = {
        row: sourceField.layoutRow,
        column: sourceField.layoutColumn,
      };
      const targetField = currentFields.find((field) =>
        field.clientId !== sourceClientId &&
        field.scope === scope &&
        fieldOccupiesTemplateCell(field, row, column));

      return renumberTemplateFields(
        currentFields.map((field) => {
          if (field.clientId === sourceClientId) {
            return normalizeFieldToLayoutRows(
              {
                ...field,
                layoutRow: row,
                layoutColumn: column,
                layoutColumnSpan: 1,
              },
              layoutRows[scope],
            );
          }

          if (targetField && field.clientId === targetField.clientId) {
            return normalizeFieldToLayoutRows(
              {
                ...field,
                layoutRow: sourceCell.row,
                layoutColumn: sourceCell.column,
              },
              layoutRows[scope],
            );
          }

          return field;
        }),
      );
    });
    setDraggedFieldId(null);
  };

  const saveFieldModal = () => {
    if (!fieldModal) {
      return;
    }

    const trimmedName = fieldDraft.name.trim();
    if (!trimmedName) {
      return;
    }

    const layoutWeight = normalizeLayoutWeight(
      layoutWeights[fieldModal.scope]?.[fieldModal.row - 1]?.[fieldModal.column - 1],
    );

    if (fieldModal.clientId) {
      updateField(fieldModal.clientId, {
        name: trimmedName,
        optionsText: fieldDraft.optionsText,
        required: fieldDraft.required,
        type: fieldDraft.type,
      });
      setFieldModal(null);
      return;
    }

    const clientId = crypto.randomUUID();
    setFields((currentFields) =>
      renumberTemplateFields([
        ...currentFields.filter(
          (field) => !(
            field.scope === fieldModal.scope &&
            fieldOccupiesTemplateCell(field, fieldModal.row, fieldModal.column)
          ),
        ),
        normalizeFieldToLayoutRows({
          clientId,
          name: trimmedName,
          type: fieldDraft.type,
          scope: fieldModal.scope,
          required: fieldDraft.required,
          sortOrder: currentFields.filter((field) => field.scope === fieldModal.scope).length,
          optionsText: fieldDraft.optionsText,
          layoutRow: fieldModal.row,
          layoutColumn: fieldModal.column,
          layoutRowSpan: 1,
          layoutColumnSpan: 1,
          layoutWeight,
        }, layoutRows[fieldModal.scope]),
      ]),
    );
    setActiveFieldId(clientId);
    setFieldModal(null);
  };

  const closeFieldModal = () => {
    setFieldModal(null);
    setActiveFieldId(null);
  };

  const removeFieldFromModal = () => {
    if (!fieldModal?.clientId) {
      closeFieldModal();
      return;
    }

    removeField(fieldModal.clientId);
    closeFieldModal();
  };

  const updateRowWeights = (
    scope: FieldDefinitionScope,
    rowIndex: number,
    nextWeights: number[],
  ) => {
    const normalizedWeights = normalizeColumnWeights(
      nextWeights,
      layoutRows[scope][rowIndex] ?? 1,
    );

    setLayoutWeights((currentWeights) => ({
      ...currentWeights,
      [scope]: currentWeights[scope].map((weights, index) =>
        index === rowIndex ? normalizedWeights : weights),
    }));
    setFields((currentFields) =>
      currentFields.map((field) =>
        field.scope === scope && field.layoutRow === rowIndex + 1
          ? {
              ...field,
              layoutWeight: normalizeLayoutWeight(
                normalizedWeights[field.layoutColumn - 1] ?? field.layoutWeight,
              ),
            }
          : field),
    );
  };

  const updateLayoutRowsForScope = (
    scope: FieldDefinitionScope,
    getNextRows: (rows: number[]) => number[],
  ) => {
    setLayoutRows((currentRows) => {
      const nextScopeRows = getNextRows(currentRows[scope]).map((columnCount) =>
        clampInteger(columnCount, 1, FIELD_LAYOUT_MAX_COLUMNS));

      setLayoutWeights((currentWeights) => ({
        ...currentWeights,
        [scope]: nextScopeRows.map((columnCount, rowIndex) =>
          normalizeColumnWeights(currentWeights[scope]?.[rowIndex], columnCount)),
      }));
      setLayoutHeights((currentHeights) => ({
        ...currentHeights,
        [scope]: nextScopeRows.map((_, rowIndex) =>
          normalizeLayoutHeight(currentHeights[scope]?.[rowIndex])),
      }));
      setFields((currentFields) =>
        currentFields.map((field) =>
          field.scope === scope
            ? normalizeFieldToLayoutRows(field, nextScopeRows)
            : field,
        ),
      );

      return {
        ...currentRows,
        [scope]: nextScopeRows,
      };
    });
  };

  const splitLayoutCell = (
    scope: FieldDefinitionScope,
    rowIndex: number,
    column: number,
  ) => {
    const rowNumber = rowIndex + 1;
    const currentColumnCount = layoutRows[scope][rowIndex] ?? 1;

    if (currentColumnCount >= FIELD_LAYOUT_MAX_COLUMNS) {
      return;
    }

    const nextRowWeights = insertColumnWeightAfter(
      normalizeColumnWeights(layoutWeights[scope][rowIndex], currentColumnCount),
      column - 1,
    );

    setLayoutRows((currentRows) => ({
      ...currentRows,
      [scope]: currentRows[scope].map((columnCount, index) =>
        index === rowIndex ? columnCount + 1 : columnCount),
    }));
    setLayoutWeights((currentWeights) => ({
      ...currentWeights,
      [scope]: currentWeights[scope].map((weights, index) =>
        index === rowIndex
          ? nextRowWeights
          : weights),
    }));

    setFields((currentFields) =>
      renumberTemplateFields(
        currentFields.map((field) => {
          if (
            field.scope !== scope ||
            field.layoutRow !== rowNumber ||
            field.layoutColumn <= column
          ) {
            return field;
          }

          return {
            ...field,
            layoutColumn: field.layoutColumn + 1,
            layoutWeight: normalizeLayoutWeight(nextRowWeights[field.layoutColumn] ?? field.layoutWeight),
          };
        }),
      ),
    );
  };

  const setLayoutRowCount = (scope: FieldDefinitionScope, rowCount: number) => {
    const highestFieldRow = Math.max(
      1,
      ...fields
        .filter((field) => field.scope === scope)
        .map((field) => field.layoutRow),
    );
    const nextRowCount = Math.max(
      highestFieldRow,
      clampInteger(rowCount, 1, 12),
    );

    updateLayoutRowsForScope(scope, (rows) => {
      if (nextRowCount > rows.length) {
        return [
          ...rows,
          ...Array.from(
            { length: nextRowCount - rows.length },
            () => 1,
          ),
        ];
      }

      return rows.slice(0, nextRowCount);
    });
  };

  const removeLayoutRow = (
    scope: FieldDefinitionScope,
    rowIndex: number,
    force = false,
  ) => {
    const rowNumber = rowIndex + 1;
    const rowFields = fields.filter(
      (field) => field.scope === scope && field.layoutRow === rowNumber,
    );

    if (layoutRows[scope].length <= 1) {
      return;
    }

    if (rowFields.length > 0 && !force) {
      setRowRemoval({
        fieldNames: rowFields.map((field) => field.name),
        rowIndex,
        scope,
      });
      return;
    }

    setLayoutRows((currentRows) => ({
      ...currentRows,
      [scope]: currentRows[scope].filter((_, index) => index !== rowIndex),
    }));
    setLayoutWeights((currentWeights) => ({
      ...currentWeights,
      [scope]: currentWeights[scope].filter((_, index) => index !== rowIndex),
    }));
    setLayoutHeights((currentHeights) => ({
      ...currentHeights,
      [scope]: currentHeights[scope].filter((_, index) => index !== rowIndex),
    }));
    setFields((currentFields) =>
      renumberTemplateFields(
        currentFields
          .filter((field) => !(field.scope === scope && field.layoutRow === rowNumber))
          .map((field) =>
            field.scope === scope && field.layoutRow > rowNumber
              ? {
                  ...field,
                  layoutRow: field.layoutRow - 1,
                }
              : field),
      ),
    );
  };

  const removeField = (clientId: string) => {
    setFields((currentFields) =>
      renumberTemplateFields(
        currentFields.filter((field) => field.clientId !== clientId),
      ),
    );
    setActiveFieldId((currentActiveFieldId) =>
      currentActiveFieldId === clientId ? null : currentActiveFieldId);
  };

  const updateLayoutRowHeight = (
    scope: FieldDefinitionScope,
    rowIndex: number,
    height: number,
  ) => {
    setLayoutHeights((currentHeights) => ({
      ...currentHeights,
      [scope]: currentHeights[scope].map((currentHeight, index) =>
        index === rowIndex ? normalizeLayoutHeight(height) : currentHeight),
    }));
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const trimmedName = name.trim();
    if (!trimmedName) {
      return;
    }

    const fieldsForSave = renumberTemplateFields(
      fields.map((field) => normalizeFieldToLayoutRows(
        {
          ...field,
          layoutWeight: normalizeLayoutWeight(
            layoutWeights[field.scope]?.[field.layoutRow - 1]?.[field.layoutColumn - 1] ??
              field.layoutWeight,
          ),
        },
        layoutRows[field.scope],
      )),
    );
    const layout = buildTemplateLayoutRequest(
      layoutRows,
      layoutWeights,
      layoutHeights,
    );

    setIsSubmitting(true);
    await onSaveTemplate(
      template?.id ?? null,
      trimmedName,
      fieldsForSave.map((field) => ({
        id: field.id ?? null,
        name: field.name.trim(),
        type: field.type,
        scope: field.scope,
        required: field.required,
        sortOrder: field.sortOrder,
        options: field.type === 'Select' ? splitOptions(field.optionsText) : [],
        layoutRow: field.layoutRow,
        layoutColumn: field.layoutColumn,
        layoutRowSpan: field.layoutRowSpan,
        layoutColumnSpan: 1,
        layoutWeight: field.layoutWeight,
      })),
      layout,
    );
    setIsSubmitting(false);
  };

  const entryPreviewFields = (
    [...fields.filter((field) => field.scope === 'Entry')]
      .sort((first, second) => first.sortOrder - second.sortOrder)
      .map((field) => normalizeFieldToLayoutRows(field, layoutRows.Entry))
  );
  const headerPreviewFields = (
    [...fields.filter((field) => field.scope === 'Header')]
      .sort((first, second) => first.sortOrder - second.sortOrder)
      .map((field) => normalizeFieldToLayoutRows(field, layoutRows.Header))
  );

  return (
    <form className="template-editor" onSubmit={handleSubmit}>
      <div className="detail-header">
        <div>
          <p className="detail-kicker">{template ? 'Edit template' : 'New template'}</p>
          <h2>{template?.name ?? 'New template'}</h2>
        </div>
        {template ? (
          <button
            className="secondary-action"
            onClick={() => void onDeleteTemplate(template.id)}
            type="button"
          >
            <Icon name="trash" />
            <span>Delete</span>
          </button>
        ) : null}
      </div>

      <label className="template-name">
        Name
        <input
          placeholder="Template name"
          onChange={(event) => setName(event.target.value)}
          required
          type="text"
          value={name}
        />
      </label>

      <section className="template-field-scope">
        <TemplateScopeHelp />
        <div className="section-heading">
          <span>
            <h3>Task header fields</h3>
            <small>Click a cell to define a task-level field.</small>
          </span>
        </div>
        <TemplateLayoutCanvas
          activeFieldId={activeFieldId}
          draggedFieldId={draggedFieldId}
          emptyLabel="Title only"
          fields={headerPreviewFields}
          layoutHeights={layoutHeights.Header}
          layoutRows={layoutRows.Header}
          layoutWeights={layoutWeights.Header}
          onChangeRowCount={(rowCount) => setLayoutRowCount('Header', rowCount)}
          onChangeRowHeight={(rowIndex, height) =>
            updateLayoutRowHeight('Header', rowIndex, height)}
          onChangeRowWeights={(rowIndex, weights) =>
            updateRowWeights('Header', rowIndex, weights)}
          onCreateField={(row, column) => openFieldModal('Header', row, column)}
          onDropField={(sourceClientId, row, column) =>
            moveFieldToLayoutCell(sourceClientId, 'Header', row, column)}
          onEndDrag={() => setDraggedFieldId(null)}
          onRemoveRow={(rowIndex) => removeLayoutRow('Header', rowIndex)}
          onRemoveColumn={(rowIndex, boundaryIndex) =>
            removeLayoutColumn('Header', rowIndex, boundaryIndex)}
          onSelectField={(field) => openFieldModal(field.scope, field.layoutRow, field.layoutColumn)}
          onSplitCell={(rowIndex, column) =>
            splitLayoutCell('Header', rowIndex, column)}
          onStartDrag={setDraggedFieldId}
        />
      </section>

      <section className="template-field-scope">
        <TemplateScopeHelp />
        <div className="section-heading">
          <span>
            <h3>Entry fields</h3>
            <small>Click a cell to define what each note/entry captures.</small>
          </span>
        </div>
        <TemplateLayoutCanvas
          activeFieldId={activeFieldId}
          draggedFieldId={draggedFieldId}
          emptyLabel="Plain note text"
          fields={entryPreviewFields}
          layoutHeights={layoutHeights.Entry}
          layoutRows={layoutRows.Entry}
          layoutWeights={layoutWeights.Entry}
          onChangeRowCount={(rowCount) => setLayoutRowCount('Entry', rowCount)}
          onChangeRowHeight={(rowIndex, height) =>
            updateLayoutRowHeight('Entry', rowIndex, height)}
          onChangeRowWeights={(rowIndex, weights) =>
            updateRowWeights('Entry', rowIndex, weights)}
          onCreateField={(row, column) => openFieldModal('Entry', row, column)}
          onDropField={(sourceClientId, row, column) =>
            moveFieldToLayoutCell(sourceClientId, 'Entry', row, column)}
          onEndDrag={() => setDraggedFieldId(null)}
          onRemoveRow={(rowIndex) => removeLayoutRow('Entry', rowIndex)}
          onRemoveColumn={(rowIndex, boundaryIndex) =>
            removeLayoutColumn('Entry', rowIndex, boundaryIndex)}
          onSelectField={(field) => openFieldModal(field.scope, field.layoutRow, field.layoutColumn)}
          onSplitCell={(rowIndex, column) =>
            splitLayoutCell('Entry', rowIndex, column)}
          onStartDrag={setDraggedFieldId}
        />
      </section>

      <div className="dialog-actions">
        <button disabled={!name.trim() || isSubmitting} type="submit">
          Save template
        </button>
      </div>
      {fieldModal ? (
        <TemplateFieldDialog
          draft={fieldDraft}
          isExistingField={Boolean(fieldModal.clientId)}
          onChange={setFieldDraft}
          onClose={closeFieldModal}
          onRemove={removeFieldFromModal}
          onSave={saveFieldModal}
          positionLabel={`Row ${fieldModal.row}, cell ${fieldModal.column}`}
        />
      ) : null}
      {columnRemoval ? (
        <TemplateColumnRemovalDialog
          fieldNames={columnRemoval.fieldNames}
          onClose={() => setColumnRemoval(null)}
          onConfirm={() => {
            removeLayoutColumn(
              columnRemoval.scope,
              columnRemoval.rowIndex,
              columnRemoval.boundaryIndex,
              true,
            );
            setColumnRemoval(null);
          }}
        />
      ) : null}
      {rowRemoval ? (
        <TemplateRowRemovalDialog
          fieldNames={rowRemoval.fieldNames}
          onClose={() => setRowRemoval(null)}
          onConfirm={() => {
            removeLayoutRow(
              rowRemoval.scope,
              rowRemoval.rowIndex,
              true,
            );
            setRowRemoval(null);
          }}
        />
      ) : null}
    </form>
  );
}

function TemplateScopeHelp() {
  return (
    <span className="template-help">
      <button className="template-help-button" type="button" aria-label="Template builder help">
        ?
      </button>
      <span className="template-help-tooltip" role="tooltip">
        Add rows, split a row by clicking a faint divider, drag the solid dividers to resize, then click a cell to choose its field label and type.
      </span>
    </span>
  );
}

function TemplateFieldDialog({
  draft,
  isExistingField,
  onChange,
  onClose,
  onRemove,
  onSave,
  positionLabel,
}: {
  draft: TemplateFieldDraft;
  isExistingField: boolean;
  onChange: (draft: TemplateFieldDraft) => void;
  onClose: () => void;
  onRemove: () => void;
  onSave: () => void;
  positionLabel: string;
}) {
  return (
    <div
      className="dialog-backdrop template-field-dialog-backdrop"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) {
          onClose();
        }
      }}
    >
      <section className="template-field-dialog" onMouseDown={(event) => event.stopPropagation()}>
        <div className="detail-header">
          <div>
            <p className="detail-kicker">{positionLabel}</p>
            <h2>{isExistingField ? 'Edit field' : 'New field'}</h2>
          </div>
          <button className="tiny-icon-button" onClick={onClose} title="Close" type="button">
            <Icon name="close" />
          </button>
        </div>
        <label>
          Field label
          <input
            autoFocus
            onChange={(event) => onChange({ ...draft, name: event.target.value })}
            placeholder="Example: Done, Person, Next step"
            required
            type="text"
            value={draft.name}
          />
        </label>
        <label>
          Type
          <select
            onChange={(event) =>
              onChange({ ...draft, type: event.target.value as FieldDefinitionType })}
            value={draft.type}
          >
            {fieldTypes.map((fieldType) => (
              <option key={fieldType} value={fieldType}>
                {fieldType}
              </option>
            ))}
          </select>
        </label>
        <label className="checkbox-label">
          <input
            checked={draft.required}
            onChange={(event) => onChange({ ...draft, required: event.target.checked })}
            type="checkbox"
          />
          Required
        </label>
        {draft.type === 'Select' ? (
          <label className="template-cell-options">
            Options
            <textarea
              onChange={(event) => onChange({ ...draft, optionsText: event.target.value })}
              placeholder="One option per line"
              rows={4}
              value={draft.optionsText}
            />
          </label>
        ) : null}
        <div className="dialog-actions">
          {isExistingField ? (
            <button className="danger-action" onClick={onRemove} type="button">
              <Icon name="trash" />
              <span>Remove field</span>
            </button>
          ) : null}
          <button className="secondary-action" onClick={onClose} type="button">
            Cancel
          </button>
          <button disabled={!draft.name.trim()} onClick={onSave} type="button">
            Save field
          </button>
        </div>
      </section>
    </div>
  );
}

function TemplateColumnRemovalDialog({
  fieldNames,
  onClose,
  onConfirm,
}: {
  fieldNames: string[];
  onClose: () => void;
  onConfirm: () => void;
}) {
  return (
    <div
      className="dialog-backdrop template-field-dialog-backdrop"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) {
          onClose();
        }
      }}
    >
      <section className="template-field-dialog" onMouseDown={(event) => event.stopPropagation()}>
        <div className="detail-header">
          <div>
            <p className="detail-kicker">Remove divider</p>
            <h2>Field in the cell to the right</h2>
          </div>
          <button className="tiny-icon-button" onClick={onClose} title="Close" type="button">
            <Icon name="close" />
          </button>
        </div>
        <p className="empty-copy">
          Removing this divider also removes {fieldNames.join(', ')} from this template row.
        </p>
        <div className="dialog-actions">
          <button className="secondary-action" onClick={onClose} type="button">
            Cancel
          </button>
          <button className="danger-action" onClick={onConfirm} type="button">
            <Icon name="trash" />
            <span>Remove divider</span>
          </button>
        </div>
      </section>
    </div>
  );
}

function TemplateRowRemovalDialog({
  fieldNames,
  onClose,
  onConfirm,
}: {
  fieldNames: string[];
  onClose: () => void;
  onConfirm: () => void;
}) {
  return (
    <div
      className="dialog-backdrop template-field-dialog-backdrop"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) {
          onClose();
        }
      }}
    >
      <section className="template-field-dialog" onMouseDown={(event) => event.stopPropagation()}>
        <div className="detail-header">
          <div>
            <p className="detail-kicker">Remove row</p>
            <h2>Fields in this row</h2>
          </div>
          <button className="tiny-icon-button" onClick={onClose} title="Close" type="button">
            <Icon name="close" />
          </button>
        </div>
        <p className="empty-copy">
          Removing this row also removes {fieldNames.join(', ')} from this template.
        </p>
        <div className="dialog-actions">
          <button className="secondary-action" onClick={onClose} type="button">
            Cancel
          </button>
          <button className="danger-action" onClick={onConfirm} type="button">
            <Icon name="trash" />
            <span>Remove row</span>
          </button>
        </div>
      </section>
    </div>
  );
}

function TemplateLayoutFieldPreview({
  type,
}: {
  type: FieldDefinitionType;
}) {
  if (type === 'Checkbox') {
    return (
      <span className="template-layout-field-preview is-checkbox">
        <span />
        <small>Checkbox</small>
      </span>
    );
  }

  if (type === 'LongText') {
    return (
      <span className="template-layout-field-preview is-long-text">
        <span />
        <span />
        <span />
      </span>
    );
  }

  if (type === 'Date') {
    return (
      <span className="template-layout-field-preview is-pill">
        Date
      </span>
    );
  }

  if (type === 'Select') {
    return (
      <span className="template-layout-field-preview is-pill">
        Select
      </span>
    );
  }

  return (
    <span className="template-layout-field-preview is-text">
      <span />
    </span>
  );
}

function TemplateLayoutCanvas({
  activeFieldId,
  draggedFieldId,
  emptyLabel,
  fields,
  layoutHeights,
  layoutRows,
  layoutWeights,
  onChangeRowCount,
  onChangeRowHeight,
  onChangeRowWeights,
  onCreateField,
  onDropField,
  onEndDrag,
  onRemoveColumn,
  onRemoveRow,
  onSelectField,
  onSplitCell,
  onStartDrag,
}: {
  activeFieldId: string | null;
  draggedFieldId: string | null;
  emptyLabel: string;
  fields: EditableTemplateField[];
  layoutHeights: number[];
  layoutRows: number[];
  layoutWeights: number[][];
  onChangeRowCount: (rowCount: number) => void;
  onChangeRowHeight: (rowIndex: number, height: number) => void;
  onChangeRowWeights: (rowIndex: number, weights: number[]) => void;
  onCreateField: (row: number, column: number) => void;
  onDropField: (sourceClientId: string, row: number, column: number) => void;
  onEndDrag: () => void;
  onRemoveColumn: (rowIndex: number, boundaryIndex: number) => void;
  onRemoveRow: (rowIndex: number) => void;
  onSelectField: (field: EditableTemplateField) => void;
  onSplitCell: (rowIndex: number, column: number) => void;
  onStartDrag: (clientId: string) => void;
}) {
  const [lastResizeDragEndedAt, setLastResizeDragEndedAt] = useState(0);

  const handleCellDrop = (
    event: DragEvent<HTMLButtonElement>,
    row: number,
    column: number,
  ) => {
    event.preventDefault();
    const sourceClientId =
      event.dataTransfer.getData('text/plain') || draggedFieldId;

    if (sourceClientId) {
      onDropField(sourceClientId, row, column);
    }
  };

  const splitCell = (rowIndex: number, column: number) => {
    const columnCount = layoutRows[rowIndex] ?? 1;

    if (columnCount >= FIELD_LAYOUT_MAX_COLUMNS) {
      return;
    }

    onSplitCell(rowIndex, column);
  };

  const startColumnResize = (
    event: ReactPointerEvent<HTMLButtonElement>,
    rowIndex: number,
    boundaryIndex: number,
  ) => {
    event.preventDefault();
    event.stopPropagation();

    const rowElement = event.currentTarget.closest('.template-layout-grid-row');

    if (!(rowElement instanceof HTMLElement)) {
      return;
    }

    const rowBounds = rowElement.getBoundingClientRect();
    const initialWeights = normalizeColumnWeights(
      layoutWeights[rowIndex],
      layoutRows[rowIndex] ?? 1,
    );
    const initialClientX = event.clientX;
    const totalWeight = initialWeights.reduce((sum, weight) => sum + weight, 0);
    const weightBeforePair = initialWeights
      .slice(0, boundaryIndex)
      .reduce((sum, weight) => sum + weight, 0);
    const pairWeight = initialWeights[boundaryIndex] + initialWeights[boundaryIndex + 1];

    const handlePointerMove = (moveEvent: PointerEvent) => {
      if (Math.abs(moveEvent.clientX - initialClientX) > 3) {
        setLastResizeDragEndedAt(Date.now());
      }

      const pointerRatio = Math.min(
        0.995,
        Math.max(0.005, (moveEvent.clientX - rowBounds.left) / rowBounds.width),
      );
      const pointerWeight = pointerRatio * totalWeight;
      const nextLeftWeight = Math.min(
        pairWeight - minimumColumnWeight,
        Math.max(minimumColumnWeight, pointerWeight - weightBeforePair),
      );
      const nextRightWeight = pairWeight - nextLeftWeight;

      const nextWeights = [...initialWeights];
      nextWeights[boundaryIndex] = nextLeftWeight;
      nextWeights[boundaryIndex + 1] = nextRightWeight;
      onChangeRowWeights(rowIndex, nextWeights);
    };

    const handlePointerUp = () => {
      window.removeEventListener('pointermove', handlePointerMove);
      window.removeEventListener('pointerup', handlePointerUp);
    };

    window.addEventListener('pointermove', handlePointerMove);
    window.addEventListener('pointerup', handlePointerUp);
  };

  const startRowResize = (
    event: ReactPointerEvent<HTMLButtonElement>,
    rowIndex: number,
  ) => {
    event.preventDefault();
    event.stopPropagation();

    const initialClientY = event.clientY;
    const initialHeight = normalizeLayoutHeight(layoutHeights[rowIndex]);

    const handlePointerMove = (moveEvent: PointerEvent) => {
      const nextHeight = initialHeight + moveEvent.clientY - initialClientY;
      onChangeRowHeight(rowIndex, nextHeight);
      setLastResizeDragEndedAt(Date.now());
    };

    const handlePointerUp = () => {
      window.removeEventListener('pointermove', handlePointerMove);
      window.removeEventListener('pointerup', handlePointerUp);
    };

    window.addEventListener('pointermove', handlePointerMove);
    window.addEventListener('pointerup', handlePointerUp);
  };

  return (
    <div className="template-layout-designer">
      <div className="template-layout-toolbar">
        <TemplateLayoutStepper
          label="Rows"
          max={12}
          min={1}
          onChange={onChangeRowCount}
          value={layoutRows.length}
        />
        <span>Build rows first. Split a row into cells, then click a cell to define its field.</span>
      </div>
      <div className="template-layout-rows">
        {layoutRows.map((columnCount, rowIndex) => {
          const rowNumber = rowIndex + 1;
          const rowFields = fields.filter((field) => field.layoutRow === rowNumber);
          const weights = normalizeColumnWeights(layoutWeights[rowIndex], columnCount);
          const rowHeight = normalizeLayoutHeight(
            layoutHeights[rowIndex],
            getDefaultRowHeight(rowFields),
          );

          return (
            <div className="template-layout-row" key={rowNumber}>
              <div className="template-layout-row-header">
                <strong>Row {rowNumber}</strong>
                <div className="template-layout-row-actions">
                  <span>{columnCount} {columnCount === 1 ? 'cell' : 'cells'}</span>
                  <button
                    className="tiny-icon-button"
                    disabled={layoutRows.length <= 1}
                    onClick={() => onRemoveRow(rowIndex)}
                    title="Remove row"
                    type="button"
                  >
                    <Icon name="trash" />
                  </button>
                </div>
              </div>
              <div
                className="template-layout-grid-row"
                style={{
                  '--template-layout-columns': columnCount,
                  '--template-layout-row-height': `${Math.round(rowHeight)}px`,
                  gridTemplateColumns: weights.map((weight) => `${weight}fr`).join(' '),
                  minHeight: `var(--template-layout-row-height)`,
                } as CSSProperties}
              >
                {Array.from({ length: columnCount }, (_, columnIndex) => {
                  const columnNumber = columnIndex + 1;
                  const field = rowFields.find(
                    (rowField) => rowField.layoutColumn === columnNumber,
                  );

                  if (field) {
                    return (
                      <button
                        className="template-layout-field-cell"
                        data-active={activeFieldId === field.clientId}
                        data-field-type={field.type}
                        draggable
                        key={field.clientId}
                        onClick={(event) => {
                          event.stopPropagation();
                          onSelectField(field);
                        }}
                        onDragEnd={onEndDrag}
                        onDragOver={(event) => event.preventDefault()}
                        onDragStart={(event) => {
                          onStartDrag(field.clientId);
                          event.dataTransfer.effectAllowed = 'move';
                          event.dataTransfer.setData('text/plain', field.clientId);
                        }}
                        onDrop={(event) =>
                          handleCellDrop(event, field.layoutRow, field.layoutColumn)}
                        style={{
                          gridColumn: field.layoutColumn,
                        }}
                        type="button"
                      >
                        <span className="template-layout-field-label">{field.name}</span>
                        <span className="template-layout-field-meta">
                          {field.type}
                        </span>
                        <TemplateLayoutFieldPreview type={field.type} />
                      </button>
                    );
                  }

                  return (
                    <button
                      aria-label={`Drop field in row ${rowNumber}, column ${columnNumber}`}
                      className="template-layout-cell template-layout-empty-cell"
                      key={`${rowNumber}:${columnNumber}`}
                      onClick={() => onCreateField(rowNumber, columnNumber)}
                      onDragOver={(event) => event.preventDefault()}
                      onDrop={(event) => handleCellDrop(event, rowNumber, columnNumber)}
                      type="button"
                    >
                      <Icon name="plus" />
                      <span>Field</span>
                      <small>{rowFields.length === 0 && columnNumber === 1 ? emptyLabel : `Cell ${columnNumber}`}</small>
                    </button>
                  );
                })}
                {columnCount < FIELD_LAYOUT_MAX_COLUMNS
                  ? Array.from({ length: columnCount }, (_, columnIndex) => (
                      <button
                        aria-label={`Split row ${rowNumber}, cell ${columnIndex + 1}`}
                        className="template-layout-split-guide"
                        key={`split:${rowNumber}:${columnIndex + 1}`}
                        onClick={(event) => {
                          event.stopPropagation();
                          splitCell(rowIndex, columnIndex + 1);
                        }}
                        style={{
                          left: `${getColumnMidpointPercent(weights, columnIndex)}%`,
                        }}
                        title="Split cell"
                        type="button"
                      />
                    ))
                  : null}
                {columnCount > 1
                  ? Array.from({ length: columnCount - 1 }, (_, boundaryIndex) => (
                      <button
                        aria-label={`Resize row ${rowNumber} columns ${boundaryIndex + 1} and ${boundaryIndex + 2}`}
                        className="template-layout-resize-handle"
                        key={`resize:${rowNumber}:${boundaryIndex}`}
                        onClick={(event) => {
                          event.stopPropagation();
                          if (Date.now() - lastResizeDragEndedAt < 250) {
                            return;
                          }

                          onRemoveColumn(rowIndex, boundaryIndex);
                        }}
                        onPointerDown={(event) =>
                          startColumnResize(event, rowIndex, boundaryIndex)}
                        style={{
                          left: `${getColumnBoundaryPercent(weights, boundaryIndex)}%`,
                        }}
                        title="Drag to resize, click to remove divider"
                        type="button"
                      />
                    ))
                  : null}
                <button
                  aria-label={`Resize row ${rowNumber} height`}
                  className="template-layout-row-resize-handle"
                  onPointerDown={(event) => startRowResize(event, rowIndex)}
                  title="Drag to resize row height"
                  type="button"
                />
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

function TemplateLayoutStepper({
  label,
  max,
  min,
  onChange,
  value,
}: {
  label: string;
  max: number;
  min: number;
  onChange: (value: number) => void;
  value: number;
}) {
  const setNextValue = (nextValue: number) => {
    onChange(clampInteger(nextValue, min, max));
  };

  return (
    <label className="layout-stepper">
      <span>{label}</span>
      <span className="layout-stepper-control">
        <button
          disabled={value <= min}
          onClick={() => setNextValue(value - 1)}
          title={`${label} -`}
          type="button"
        >
          <Icon name="minus" />
        </button>
        <input
          max={max}
          min={min}
          onChange={(event) => setNextValue(event.target.valueAsNumber)}
          type="number"
          value={value}
        />
        <button
          disabled={value >= max}
          onClick={() => setNextValue(value + 1)}
          title={`${label} +`}
          type="button"
        >
          <Icon name="plus" />
        </button>
      </span>
    </label>
  );
}

