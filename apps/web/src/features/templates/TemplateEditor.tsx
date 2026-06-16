import { type CSSProperties, type DragEvent, type FormEvent, useState } from 'react';
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
  const [layoutRows, setLayoutRows] = useState<TemplateLayoutRows>(
    () => createTemplateLayoutRows(template?.fields.map(toEditableTemplateField) ?? []),
  );

  const addField = (scope: FieldDefinitionScope) => {
    setFields((currentFields) => {
      const firstOpenCell = findFirstOpenTemplateCell(
        currentFields,
        layoutRows[scope],
        scope,
      );

      return [
        ...currentFields,
        {
        clientId: crypto.randomUUID(),
        name: 'New field',
        type: 'Text',
        scope,
        required: false,
        sortOrder: currentFields.filter((field) => field.scope === scope).length,
        optionsText: '',
        layoutRow: firstOpenCell.row,
        layoutColumn: firstOpenCell.column,
        layoutRowSpan: 1,
        layoutColumnSpan: 1,
        },
      ];
    });
  };

  const updateField = (
    clientId: string,
    update: Partial<EditableTemplateField>,
  ) => {
    setFields((currentFields) =>
      currentFields.map((field) =>
        field.clientId === clientId ? { ...field, ...update } : field,
      ),
    );
  };

  const moveField = (clientId: string, direction: -1 | 1) => {
    setFields((currentFields) => {
      const fieldToMove = currentFields.find((field) => field.clientId === clientId);

      if (!fieldToMove) {
        return currentFields;
      }

      const scopedFields = currentFields.filter(
        (field) => field.scope === fieldToMove.scope,
      );
      const scopedIndex = scopedFields.findIndex((field) => field.clientId === clientId);
      const nextScopedIndex = scopedIndex + direction;

      if (
        scopedIndex < 0 ||
        nextScopedIndex < 0 ||
        nextScopedIndex >= scopedFields.length
      ) {
        return currentFields;
      }

      const reorderedScopedFields = [...scopedFields];
      const [field] = reorderedScopedFields.splice(scopedIndex, 1);
      reorderedScopedFields.splice(nextScopedIndex, 0, field);

      const mergedFields = currentFields.map((currentField) =>
        currentField.scope === fieldToMove.scope
          ? reorderedScopedFields.shift()!
          : currentField,
      );

      return renumberTemplateFields(mergedFields);
    });
  };

  const moveFieldTo = (sourceClientId: string, targetClientId: string) => {
    if (sourceClientId === targetClientId) {
      return;
    }

    setFields((currentFields) => {
      const sourceField = currentFields.find((field) => field.clientId === sourceClientId);
      const targetField = currentFields.find((field) => field.clientId === targetClientId);

      if (!sourceField || !targetField || sourceField.scope !== targetField.scope) {
        return currentFields;
      }

      const scopedFields = currentFields.filter(
        (field) => field.scope === sourceField.scope,
      );
      const sourceIndex = scopedFields.findIndex(
        (field) => field.clientId === sourceClientId,
      );
      const targetIndex = scopedFields.findIndex(
        (field) => field.clientId === targetClientId,
      );

      if (sourceIndex < 0 || targetIndex < 0) {
        return currentFields;
      }

      const reorderedScopedFields = [...scopedFields];
      const [field] = reorderedScopedFields.splice(sourceIndex, 1);
      reorderedScopedFields.splice(targetIndex, 0, field);

      const mergedFields = currentFields.map((currentField) =>
        currentField.scope === sourceField.scope
          ? reorderedScopedFields.shift()!
          : currentField,
      );

      return renumberTemplateFields(mergedFields);
    });
  };

  const handleFieldDrop = (
    event: DragEvent<HTMLDivElement>,
    targetClientId: string,
  ) => {
    event.preventDefault();
    const sourceClientId =
      event.dataTransfer.getData('text/plain') || draggedFieldId;

    if (sourceClientId) {
      moveFieldTo(sourceClientId, targetClientId);
    }

    setDraggedFieldId(null);
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

  const addLayoutRow = (scope: FieldDefinitionScope) => {
    updateLayoutRowsForScope(scope, (rows) => [...rows, 1]);
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

  const renderFieldRows = (scope: FieldDefinitionScope) => {
    const scopedFields = [...fields.filter((field) => field.scope === scope)].sort(
      (first, second) => first.sortOrder - second.sortOrder,
    );

    if (scopedFields.length === 0) {
      return <p className="empty-copy">No fields yet.</p>;
    }

    return scopedFields.map((field, index) => (
      <div
        className="template-field-row"
        data-dragging={draggedFieldId === field.clientId}
        key={field.clientId}
        onDragOver={(event) => event.preventDefault()}
        onDrop={(event) => handleFieldDrop(event, field.clientId)}
      >
        <button
          className="field-drag-handle"
          draggable
          onDragEnd={() => setDraggedFieldId(null)}
          onDragStart={(event) => {
            setDraggedFieldId(field.clientId);
            event.dataTransfer.effectAllowed = 'move';
            event.dataTransfer.setData('text/plain', field.clientId);
          }}
          title="Drag to reorder"
          type="button"
        >
          <Icon name="list" />
          <span className="sr-only">Drag to reorder</span>
        </button>
        <input
          aria-label="Field name"
          onChange={(event) =>
            updateField(field.clientId, { name: event.target.value })
          }
          required
          type="text"
          value={field.name}
        />

        <select
          aria-label="Field type"
          onChange={(event) => {
            const nextType = event.target.value as FieldDefinitionType;
            updateField(field.clientId, {
              type: nextType,
              layoutColumnSpan:
                nextType === 'LongText' && field.layoutColumnSpan === 1
                  ? 2
                  : field.layoutColumnSpan,
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

        <label className="checkbox-label">
          <input
            checked={field.required}
            onChange={(event) =>
              updateField(field.clientId, { required: event.target.checked })
            }
            type="checkbox"
          />
          Required
        </label>

        <div className="field-layout-actions" aria-label="Field layout">
          <span className="field-cell-copy">
            Row {field.layoutRow}, cell {field.layoutColumn}
          </span>
          <TemplateLayoutStepper
            label="Width"
            max={Math.max(
              1,
              (layoutRows[field.scope][field.layoutRow - 1] ?? FIELD_LAYOUT_MAX_COLUMNS) -
                field.layoutColumn +
                1,
            )}
            min={1}
            onChange={(value) => updateField(field.clientId, { layoutColumnSpan: value })}
            value={field.layoutColumnSpan}
          />
        </div>

        <div className="field-order-actions">
          <button
            disabled={index === 0}
            onClick={() => moveField(field.clientId, -1)}
            title="Move up"
            type="button"
          >
            <Icon name="arrowUp" />
            <span className="sr-only">Move up</span>
          </button>
          <button
            disabled={index === scopedFields.length - 1}
            onClick={() => moveField(field.clientId, 1)}
            title="Move down"
            type="button"
          >
            <Icon name="arrowDown" />
            <span className="sr-only">Move down</span>
          </button>
          <button
            className="ghost-button"
            onClick={() => removeField(field.clientId)}
            title="Remove field"
            type="button"
          >
            <Icon name="trash" />
            <span className="sr-only">Remove field</span>
          </button>
        </div>

        {field.type === 'Select' ? (
          <label className="options-editor">
            Options
            <textarea
              onChange={(event) =>
                updateField(field.clientId, { optionsText: event.target.value })
              }
              placeholder="One option per line"
              required
              rows={3}
              value={field.optionsText}
            />
          </label>
        ) : null}
      </div>
    ));
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
        <div className="section-heading">
          <span>
            <h3>Task header fields</h3>
            <small>Fields stored on the task and used for filtering the wall.</small>
          </span>
          <button onClick={() => addField('Header')} type="button">
            <Icon name="plus" />
            <span>Add header field</span>
          </button>
        </div>
        <div className="template-fields">{renderFieldRows('Header')}</div>
        <TemplateLayoutCanvas
          draggedFieldId={draggedFieldId}
          emptyLabel="Title only"
          fields={headerPreviewFields}
          layoutRows={layoutRows.Header}
          onAddRow={() => addLayoutRow('Header')}
          onChangeRowColumns={(rowIndex, columnCount) =>
            setLayoutRowColumns('Header', rowIndex, columnCount)}
          onDropField={(sourceClientId, row, column) =>
            moveFieldToLayoutCell(sourceClientId, 'Header', row, column)}
          onEndDrag={() => setDraggedFieldId(null)}
          onRemoveRow={(rowIndex) => removeLayoutRow('Header', rowIndex)}
          onStartDrag={setDraggedFieldId}
        />
      </section>

      <section className="template-field-scope">
        <div className="section-heading">
          <span>
            <h3>Entry fields</h3>
            <small>Fields captured on each note or progress entry.</small>
          </span>
          <button onClick={() => addField('Entry')} type="button">
            <Icon name="plus" />
            <span>Add entry field</span>
          </button>
        </div>
        <div className="template-fields">{renderFieldRows('Entry')}</div>
        <TemplateLayoutCanvas
          draggedFieldId={draggedFieldId}
          emptyLabel="Plain note text"
          fields={entryPreviewFields}
          layoutRows={layoutRows.Entry}
          onAddRow={() => addLayoutRow('Entry')}
          onChangeRowColumns={(rowIndex, columnCount) =>
            setLayoutRowColumns('Entry', rowIndex, columnCount)}
          onDropField={(sourceClientId, row, column) =>
            moveFieldToLayoutCell(sourceClientId, 'Entry', row, column)}
          onEndDrag={() => setDraggedFieldId(null)}
          onRemoveRow={(rowIndex) => removeLayoutRow('Entry', rowIndex)}
          onStartDrag={setDraggedFieldId}
        />
      </section>

      <div className="dialog-actions">
        <button disabled={!name.trim() || isSubmitting} type="submit">
          Save template
        </button>
      </div>
    </form>
  );
}

function TemplateLayoutCanvas({
  draggedFieldId,
  emptyLabel,
  fields,
  layoutRows,
  onAddRow,
  onChangeRowColumns,
  onDropField,
  onEndDrag,
  onRemoveRow,
  onStartDrag,
}: {
  draggedFieldId: string | null;
  emptyLabel: string;
  fields: EditableTemplateField[];
  layoutRows: number[];
  onAddRow: () => void;
  onChangeRowColumns: (rowIndex: number, columnCount: number) => void;
  onDropField: (sourceClientId: string, row: number, column: number) => void;
  onEndDrag: () => void;
  onRemoveRow: (rowIndex: number) => void;
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

  return (
    <div className="template-layout-designer">
      <div className="template-layout-toolbar">
        <span>{layoutRows.length} row{layoutRows.length === 1 ? '' : 's'}</span>
        <button onClick={onAddRow} type="button">
          <Icon name="plus" />
          <span>Add row</span>
        </button>
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
                      data-field-type={field.type}
                      draggable
                      key={field.clientId}
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

