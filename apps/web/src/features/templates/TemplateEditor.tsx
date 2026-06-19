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
  UpsertFieldDefinitionRequest,
} from '../../types';

type TemplateLayoutRows = Record<FieldDefinitionScope, number[]>;

const templateScopes: FieldDefinitionScope[] = ['Header', 'Entry'];

function createTemplateLayoutRows(fields: EditableTemplateField[]): TemplateLayoutRows {
  return Object.fromEntries(
    templateScopes.map((scope) => {
      const scopedFields = fields.filter((field) => field.scope === scope);
      const maxRow = Math.max(
        1,
        ...scopedFields.map((field) => clampInteger(field.layoutRow, 1, 24)),
      );
      const rows = Array.from({ length: maxRow }, (_, rowIndex) => {
        const rowNumber = rowIndex + 1;
        const rowFields = scopedFields.filter(
          (field) => clampInteger(field.layoutRow, 1, 24) === rowNumber,
        );

        return Math.max(
          1,
          ...rowFields.map((field) =>
            clampInteger(field.layoutColumn, 1, FIELD_LAYOUT_MAX_COLUMNS) +
            clampInteger(field.layoutColumnSpan, 1, FIELD_LAYOUT_MAX_COLUMNS) -
            1),
        );
      });

      return [scope, rows];
    }),
  ) as TemplateLayoutRows;
}

function findFirstOpenTemplateCell(
  fields: EditableTemplateField[],
  rows: number[],
  scope: FieldDefinitionScope,
) {
  const occupiedCells = new Set<string>();

  fields
    .filter((field) => field.scope === scope)
    .forEach((field) => {
      const row = clampInteger(field.layoutRow, 1, 24);
      const column = clampInteger(field.layoutColumn, 1, FIELD_LAYOUT_MAX_COLUMNS);
      const columnSpan = clampInteger(field.layoutColumnSpan, 1, FIELD_LAYOUT_MAX_COLUMNS);

      for (let offset = 0; offset < columnSpan; offset += 1) {
        occupiedCells.add(`${row}:${column + offset}`);
      }
    });

  for (let rowIndex = 0; rowIndex < rows.length; rowIndex += 1) {
    const rowNumber = rowIndex + 1;
    const columnCount = rows[rowIndex] ?? 1;

    for (let columnNumber = 1; columnNumber <= columnCount; columnNumber += 1) {
      if (!occupiedCells.has(`${rowNumber}:${columnNumber}`)) {
        return { row: rowNumber, column: columnNumber };
      }
    }
  }

  return { row: Math.max(1, rows.length), column: 1 };
}

function fieldOccupiesTemplateCell(
  field: EditableTemplateField,
  row: number,
  column: number,
) {
  return field.layoutRow === row &&
    column >= field.layoutColumn &&
    column < field.layoutColumn + field.layoutColumnSpan;
}

function normalizeFieldToLayoutRows(
  field: EditableTemplateField,
  rows: number[],
): EditableTemplateField {
  const row = clampInteger(field.layoutRow, 1, Math.max(1, rows.length));
  const columnCount = rows[row - 1] ?? 1;
  const column = clampInteger(field.layoutColumn, 1, columnCount);
  const columnSpan = clampInteger(
    field.layoutColumnSpan,
    1,
    Math.max(1, columnCount - column + 1),
  );

  return {
    ...field,
    layoutRow: row,
    layoutColumn: column,
    layoutColumnSpan: columnSpan,
    layoutRowSpan: 1,
  };
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
  ) => Promise<TaskTemplateDetailResponse | null>;
  template: TaskTemplateDetailResponse | null;
}) {
  const [name, setName] = useState(template?.name ?? '');
  const [fields, setFields] = useState<EditableTemplateField[]>(
    () => template?.fields.map(toEditableTemplateField) ?? [],
  );
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [draggedFieldId, setDraggedFieldId] = useState<string | null>(null);
  const [activeFieldId, setActiveFieldId] = useState<string | null>(null);
  const [layoutRows, setLayoutRows] = useState<TemplateLayoutRows>(
    () => createTemplateLayoutRows(template?.fields.map(toEditableTemplateField) ?? []),
  );

  const createFieldAtCell = (
    scope: FieldDefinitionScope,
    row: number,
    column: number,
  ) => {
    const clientId = crypto.randomUUID();

    setFields((currentFields) => {
      const nextFields = [
        ...currentFields.filter(
          (field) => !(
            field.scope === scope &&
            fieldOccupiesTemplateCell(field, row, column)
          ),
        ),
        normalizeFieldToLayoutRows({
          clientId,
          name: 'New field',
          type: 'Text',
          scope,
          required: false,
          sortOrder: currentFields.filter((field) => field.scope === scope).length,
          optionsText: '',
          layoutRow: row,
          layoutColumn: column,
          layoutRowSpan: 1,
          layoutColumnSpan: 1,
        }, layoutRows[scope]),
      ];

      return renumberTemplateFields(nextFields);
    });

    setActiveFieldId(clientId);
  };

  const addField = (scope: FieldDefinitionScope) => {
    const firstOpenCell = findFirstOpenTemplateCell(
      fields,
      layoutRows[scope],
      scope,
    );

    createFieldAtCell(scope, firstOpenCell.row, firstOpenCell.column);
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

      const rowColumns = layoutRows[scope][row - 1] ?? 1;
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
                layoutColumnSpan: Math.min(field.layoutColumnSpan, rowColumns - column + 1),
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

  const updateLayoutRowsForScope = (
    scope: FieldDefinitionScope,
    getNextRows: (rows: number[]) => number[],
  ) => {
    setLayoutRows((currentRows) => {
      const nextScopeRows = getNextRows(currentRows[scope]).map((columnCount) =>
        clampInteger(columnCount, 1, FIELD_LAYOUT_MAX_COLUMNS));

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

  const setLayoutRowColumns = (
    scope: FieldDefinitionScope,
    rowIndex: number,
    columnCount: number,
  ) => {
    updateLayoutRowsForScope(scope, (rows) =>
      rows.map((existingColumnCount, index) =>
        index === rowIndex ? columnCount : existingColumnCount));
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
        const fallbackColumnCount = rows[rows.length - 1] ?? 1;

        return [
          ...rows,
          ...Array.from(
            { length: nextRowCount - rows.length },
            () => fallbackColumnCount,
          ),
        ];
      }

      return rows.slice(0, nextRowCount);
    });
  };

  const removeLayoutRow = (scope: FieldDefinitionScope, rowIndex: number) => {
    const rowNumber = rowIndex + 1;
    const rowHasFields = fields.some(
      (field) => field.scope === scope && field.layoutRow === rowNumber,
    );

    if (rowHasFields || layoutRows[scope].length <= 1) {
      return;
    }

    updateLayoutRowsForScope(scope, (rows) =>
      rows.filter((_, index) => index !== rowIndex));
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

  const resizeFieldSpan = (clientId: string, columnSpan: number) => {
    updateField(clientId, { layoutColumnSpan: columnSpan });
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const trimmedName = name.trim();
    if (!trimmedName) {
      return;
    }

    const fieldsForSave = renumberTemplateFields(
      fields.map((field) => normalizeFieldToLayoutRows(field, layoutRows[field.scope])),
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
        layoutColumnSpan: field.layoutColumnSpan,
      })),
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
  const activeHeaderField =
    headerPreviewFields.find((field) => field.clientId === activeFieldId) ?? null;
  const activeEntryField =
    entryPreviewFields.find((field) => field.clientId === activeFieldId) ?? null;

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
        <div className="section-heading">
          <span>
            <h3>Task header fields</h3>
            <small>Click a cell to define a task-level field.</small>
          </span>
          <button onClick={() => addField('Header')} type="button">
            <Icon name="plus" />
            <span>Add field</span>
          </button>
        </div>
        <TemplateLayoutCanvas
          activeFieldId={activeFieldId}
          draggedFieldId={draggedFieldId}
          emptyLabel="Title only"
          fields={headerPreviewFields}
          layoutRows={layoutRows.Header}
          onChangeRowCount={(rowCount) => setLayoutRowCount('Header', rowCount)}
          onChangeRowColumns={(rowIndex, columnCount) =>
            setLayoutRowColumns('Header', rowIndex, columnCount)}
          onCreateField={(row, column) => createFieldAtCell('Header', row, column)}
          onDropField={(sourceClientId, row, column) =>
            moveFieldToLayoutCell(sourceClientId, 'Header', row, column)}
          onEndDrag={() => setDraggedFieldId(null)}
          onRemoveRow={(rowIndex) => removeLayoutRow('Header', rowIndex)}
          onResizeField={resizeFieldSpan}
          onSelectField={setActiveFieldId}
          onStartDrag={setDraggedFieldId}
        />
        {!activeFieldId || activeHeaderField ? (
          <TemplateCellFieldEditor
            field={activeHeaderField}
            layoutRows={layoutRows.Header}
            onClose={() => setActiveFieldId(null)}
            onRemoveField={removeField}
            onUpdateField={updateField}
          />
        ) : null}
      </section>

      <section className="template-field-scope">
        <div className="section-heading">
          <span>
            <h3>Entry fields</h3>
            <small>Click a cell to define what each note/entry captures.</small>
          </span>
          <button onClick={() => addField('Entry')} type="button">
            <Icon name="plus" />
            <span>Add field</span>
          </button>
        </div>
        <TemplateLayoutCanvas
          activeFieldId={activeFieldId}
          draggedFieldId={draggedFieldId}
          emptyLabel="Plain note text"
          fields={entryPreviewFields}
          layoutRows={layoutRows.Entry}
          onChangeRowCount={(rowCount) => setLayoutRowCount('Entry', rowCount)}
          onChangeRowColumns={(rowIndex, columnCount) =>
            setLayoutRowColumns('Entry', rowIndex, columnCount)}
          onCreateField={(row, column) => createFieldAtCell('Entry', row, column)}
          onDropField={(sourceClientId, row, column) =>
            moveFieldToLayoutCell(sourceClientId, 'Entry', row, column)}
          onEndDrag={() => setDraggedFieldId(null)}
          onRemoveRow={(rowIndex) => removeLayoutRow('Entry', rowIndex)}
          onResizeField={resizeFieldSpan}
          onSelectField={setActiveFieldId}
          onStartDrag={setDraggedFieldId}
        />
        {!activeFieldId || activeEntryField ? (
          <TemplateCellFieldEditor
            field={activeEntryField}
            layoutRows={layoutRows.Entry}
            onClose={() => setActiveFieldId(null)}
            onRemoveField={removeField}
            onUpdateField={updateField}
          />
        ) : null}
      </section>

      <div className="dialog-actions">
        <button disabled={!name.trim() || isSubmitting} type="submit">
          Save template
        </button>
      </div>
    </form>
  );
}

function TemplateCellFieldEditor({
  field,
  layoutRows,
  onClose,
  onRemoveField,
  onUpdateField,
}: {
  field: EditableTemplateField | null;
  layoutRows: number[];
  onClose: () => void;
  onRemoveField: (clientId: string) => void;
  onUpdateField: (clientId: string, update: Partial<EditableTemplateField>) => void;
}) {
  if (!field) {
    return (
      <div className="template-cell-editor template-cell-editor-empty">
        <span>Click a layout cell to add a field, or click an existing field to edit it.</span>
      </div>
    );
  }

  const rowColumnCount = layoutRows[field.layoutRow - 1] ?? 1;
  const maxColumnSpan = Math.max(1, rowColumnCount - field.layoutColumn + 1);

  return (
    <div className="template-cell-editor">
      <div className="template-cell-editor-title">
        <span>
          Row {field.layoutRow}, cell {field.layoutColumn}
        </span>
        <button
          className="tiny-icon-button"
          onClick={onClose}
          title="Close"
          type="button"
        >
          <Icon name="close" />
        </button>
      </div>
      <label>
        Field label
        <input
          onChange={(event) =>
            onUpdateField(field.clientId, { name: event.target.value })}
          required
          type="text"
          value={field.name}
        />
      </label>
      <label>
        Type
        <select
          onChange={(event) => {
            const nextType = event.target.value as FieldDefinitionType;
            onUpdateField(field.clientId, {
              type: nextType,
              layoutColumnSpan:
                nextType === 'LongText'
                  ? Math.min(Math.max(2, field.layoutColumnSpan), maxColumnSpan)
                  : Math.min(field.layoutColumnSpan, maxColumnSpan),
            });
          }}
          value={field.type}
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
          checked={field.required}
          onChange={(event) =>
            onUpdateField(field.clientId, { required: event.target.checked })}
          type="checkbox"
        />
        Required
      </label>
      <TemplateLayoutStepper
        label="Width"
        max={maxColumnSpan}
        min={1}
        onChange={(value) => onUpdateField(field.clientId, { layoutColumnSpan: value })}
        value={field.layoutColumnSpan}
      />
      {field.type === 'Select' ? (
        <label className="template-cell-options">
          Options
          <textarea
            onChange={(event) =>
              onUpdateField(field.clientId, { optionsText: event.target.value })}
            placeholder="One option per line"
            rows={3}
            value={field.optionsText}
          />
        </label>
      ) : null}
      <div className="template-cell-editor-actions">
        <button
          className="secondary-action"
          onClick={() => onRemoveField(field.clientId)}
          type="button"
        >
          <Icon name="trash" />
          <span>Remove field</span>
        </button>
      </div>
    </div>
  );
}

function TemplateLayoutCanvas({
  activeFieldId,
  draggedFieldId,
  emptyLabel,
  fields,
  layoutRows,
  onChangeRowCount,
  onChangeRowColumns,
  onCreateField,
  onDropField,
  onEndDrag,
  onRemoveRow,
  onResizeField,
  onSelectField,
  onStartDrag,
}: {
  activeFieldId: string | null;
  draggedFieldId: string | null;
  emptyLabel: string;
  fields: EditableTemplateField[];
  layoutRows: number[];
  onChangeRowCount: (rowCount: number) => void;
  onChangeRowColumns: (rowIndex: number, columnCount: number) => void;
  onCreateField: (row: number, column: number) => void;
  onDropField: (sourceClientId: string, row: number, column: number) => void;
  onEndDrag: () => void;
  onRemoveRow: (rowIndex: number) => void;
  onResizeField: (clientId: string, columnSpan: number) => void;
  onSelectField: (clientId: string) => void;
  onStartDrag: (clientId: string) => void;
}) {
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

  const startFieldResize = (
    event: ReactPointerEvent<HTMLSpanElement>,
    field: EditableTemplateField,
    columnCount: number,
  ) => {
    event.preventDefault();
    event.stopPropagation();

    const gridRow = event.currentTarget.closest('.template-layout-grid-row');

    if (!(gridRow instanceof HTMLElement)) {
      return;
    }

    const startX = event.clientX;
    const cellWidth = gridRow.getBoundingClientRect().width / columnCount;
    const maxSpan = Math.max(1, columnCount - field.layoutColumn + 1);
    const startSpan = field.layoutColumnSpan;

    const handlePointerMove = (moveEvent: PointerEvent) => {
      const delta = Math.round((moveEvent.clientX - startX) / cellWidth);
      onResizeField(field.clientId, clampInteger(startSpan + delta, 1, maxSpan));
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
        <span>Click a cell to add or edit a field.</span>
      </div>
      <div className="template-layout-rows">
        {layoutRows.map((columnCount, rowIndex) => {
          const rowNumber = rowIndex + 1;
          const rowFields = fields.filter((field) => field.layoutRow === rowNumber);
          const rowHasFields = rowFields.length > 0;

          return (
            <div className="template-layout-row" key={rowNumber}>
              <div className="template-layout-row-header">
                <strong>Row {rowNumber}</strong>
                <TemplateLayoutStepper
                  label="Cols"
                  max={FIELD_LAYOUT_MAX_COLUMNS}
                  min={1}
                  onChange={(value) => onChangeRowColumns(rowIndex, value)}
                  value={columnCount}
                />
                <button
                  className="tiny-icon-button"
                  disabled={layoutRows.length <= 1 || rowHasFields}
                  onClick={() => onRemoveRow(rowIndex)}
                  title={rowHasFields ? 'Move fields before removing row' : 'Remove row'}
                  type="button"
                >
                  <Icon name="trash" />
                </button>
              </div>
              <div
                className="template-layout-grid-row"
                style={{
                  '--template-layout-columns': columnCount,
                  gridTemplateColumns: `repeat(${columnCount}, minmax(0, 1fr))`,
                } as CSSProperties}
              >
                {Array.from({ length: columnCount }, (_, columnIndex) => {
                  const columnNumber = columnIndex + 1;

                  return (
                    <button
                      aria-label={`Drop field in row ${rowNumber}, column ${columnNumber}`}
                      className="template-layout-cell"
                      key={`${rowNumber}:${columnNumber}`}
                      onClick={() => onCreateField(rowNumber, columnNumber)}
                      onDragOver={(event) => event.preventDefault()}
                      onDrop={(event) => handleCellDrop(event, rowNumber, columnNumber)}
                      type="button"
                    >
                      <span>{columnNumber}</span>
                    </button>
                  );
                })}
                {rowFields.length === 0 ? (
                  <span className="template-preview-empty">{emptyLabel}</span>
                ) : null}
                {rowFields.map((field) => {
                  const columnSpan = Math.min(
                    field.layoutColumnSpan,
                    columnCount - field.layoutColumn + 1,
                  );

                  return (
                    <button
                      className="template-preview-chip"
                      data-active={activeFieldId === field.clientId}
                      data-field-type={field.type}
                      draggable
                      key={field.clientId}
                      onClick={(event) => {
                        event.stopPropagation();
                        onSelectField(field.clientId);
                      }}
                      onDragOver={(event) => event.preventDefault()}
                      onDragEnd={onEndDrag}
                      onDragStart={(event) => {
                        onStartDrag(field.clientId);
                        event.dataTransfer.effectAllowed = 'move';
                        event.dataTransfer.setData('text/plain', field.clientId);
                      }}
                      onDrop={(event) =>
                        handleCellDrop(event, field.layoutRow, field.layoutColumn)}
                      style={{
                        gridColumn: `${field.layoutColumn} / span ${Math.max(1, columnSpan)}`,
                      }}
                      type="button"
                    >
                      <span>{field.name}</span>
                      <small>
                        {field.type} - R{field.layoutRow} C{field.layoutColumn}
                      </small>
                      <span
                        aria-hidden="true"
                        className="template-cell-resize-handle"
                        onPointerDown={(event) =>
                          startFieldResize(event, field, columnCount)}
                      />
                    </button>
                  );
                })}
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

