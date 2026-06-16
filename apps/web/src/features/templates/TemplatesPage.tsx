import { type DragEvent, type FormEvent, useEffect, useState } from 'react';
import { Icon } from '../../components/Icon';
import { fieldTypes, type EditableTemplateField } from '../../appTypes';
import { type Translate } from '../../localization';
import {
  FIELD_LAYOUT_MAX_COLUMNS,
  getEditableTemplateFieldGridStyle,
  getTemplateLayoutGridStyle,
  normalizeTemplateLayoutFields,
} from '../../templateLayout';
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

export function TemplatesPage({
  isLoading,
  onDeleteTemplate,
  onSaveTemplate,
  t,
  templates,
}: {
  isLoading: boolean;
  onDeleteTemplate: (id: string) => Promise<void>;
  onSaveTemplate: (
    id: string | null,
    name: string,
    fields: UpsertFieldDefinitionRequest[],
  ) => Promise<TaskTemplateDetailResponse | null>;
  t: Translate;
  templates: TaskTemplateDetailResponse[];
}) {
  const [selectedTemplateId, setSelectedTemplateId] = useState<string | null>(null);
  const [templateDraftIsOpen, setTemplateDraftIsOpen] = useState(false);
  const selectedTemplate =
    templateDraftIsOpen
      ? null
      : templates.find((template) => template.id === selectedTemplateId) ?? null;

  useEffect(() => {
    if (templateDraftIsOpen) {
      return;
    }

    if (selectedTemplateId && templates.some((template) => template.id === selectedTemplateId)) {
      return;
    }

    setSelectedTemplateId(templates[0]?.id ?? null);
  }, [selectedTemplateId, templateDraftIsOpen, templates]);

  const openTemplateDraft = () => {
    setSelectedTemplateId(null);
    setTemplateDraftIsOpen(true);
  };

  const selectTemplate = (templateId: string) => {
    setSelectedTemplateId(templateId);
    setTemplateDraftIsOpen(false);
  };

  const saveTemplate = async (
    id: string | null,
    templateName: string,
    fields: UpsertFieldDefinitionRequest[],
  ) => {
    const savedTemplate = await onSaveTemplate(id, templateName, fields);

    if (savedTemplate) {
      setSelectedTemplateId(savedTemplate.id);
      setTemplateDraftIsOpen(false);
    }

    return savedTemplate;
  };

  return (
    <section className="templates-page" aria-labelledby="templates-title">
      <div className="templates-list">
        <div className="board-header">
          <div>
            <p className="detail-kicker">Template structure</p>
            <h1 id="templates-title">{t('templates')}</h1>
            <p>Define reusable fields for the different shapes a task can take.</p>
          </div>
          <button onClick={openTemplateDraft} type="button">
            <Icon name="plus" />
            <span>New</span>
          </button>
        </div>

        <div className="template-picker" aria-busy={isLoading}>
          {templates.map((template) => (
            <button
              className="template-picker-row"
              data-selected={selectedTemplateId === template.id}
              key={template.id}
              onClick={() => selectTemplate(template.id)}
              type="button"
            >
              <span>{template.name}</span>
              <strong>{template.fields.length} fields</strong>
            </button>
          ))}
        </div>
      </div>

      <TemplateEditor
        key={templateDraftIsOpen ? 'new-template' : selectedTemplate?.id ?? 'empty-template'}
        onDeleteTemplate={onDeleteTemplate}
        onSaveTemplate={saveTemplate}
        template={selectedTemplate}
      />
    </section>
  );
}

function TemplateEditor({
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

  const addField = (scope: FieldDefinitionScope) => {
    setFields((currentFields) => [
      ...currentFields,
      {
        clientId: crypto.randomUUID(),
        name: 'New field',
        type: 'Text',
        scope,
        required: false,
        sortOrder: currentFields.filter((field) => field.scope === scope).length,
        optionsText: '',
        layoutRow: 1,
        layoutColumn: 1,
        layoutRowSpan: 1,
        layoutColumnSpan: 1,
      },
    ]);
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

    const fieldsForSave = normalizeTemplateLayoutFields(
      renumberTemplateFields(fields),
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
          <TemplateLayoutStepper
            label="Row"
            max={24}
            min={1}
            onChange={(value) => updateField(field.clientId, { layoutRow: value })}
            value={field.layoutRow}
          />
          <TemplateLayoutStepper
            label="Col"
            max={FIELD_LAYOUT_MAX_COLUMNS}
            min={1}
            onChange={(value) => updateField(field.clientId, { layoutColumn: value })}
            value={field.layoutColumn}
          />
          <TemplateLayoutStepper
            label="Width"
            max={FIELD_LAYOUT_MAX_COLUMNS}
            min={1}
            onChange={(value) => updateField(field.clientId, { layoutColumnSpan: value })}
            value={field.layoutColumnSpan}
          />
          <TemplateLayoutStepper
            label="Height"
            max={6}
            min={1}
            onChange={(value) => updateField(field.clientId, { layoutRowSpan: value })}
            value={field.layoutRowSpan}
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

  const entryPreviewFields = normalizeTemplateLayoutFields(
    [...fields.filter((field) => field.scope === 'Entry')]
      .sort((first, second) => first.sortOrder - second.sortOrder),
  );
  const headerPreviewFields = normalizeTemplateLayoutFields(
    [...fields.filter((field) => field.scope === 'Header')]
      .sort((first, second) => first.sortOrder - second.sortOrder),
  );
  const entryLayoutAdjusted = entryPreviewFields.some((field) => field.layoutWasAdjusted);
  const headerLayoutAdjusted = headerPreviewFields.some((field) => field.layoutWasAdjusted);

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
        <div
          className="template-scope-preview template-header-preview"
          aria-label="Header preview"
          style={getTemplateLayoutGridStyle(headerPreviewFields)}
        >
          {headerPreviewFields.length === 0 ? (
            <span className="template-preview-empty">Title only</span>
          ) : (
            headerPreviewFields.map((field) => (
              <span
                className="template-preview-chip"
                data-layout-adjusted={field.layoutWasAdjusted}
                data-field-type={field.type}
                key={field.clientId}
                style={getEditableTemplateFieldGridStyle(field)}
              >
                {field.name}
                <small>
                  {field.type} - R{field.layoutRow} C{field.layoutColumn}
                </small>
              </span>
            ))
          )}
        </div>
        {headerLayoutAdjusted ? (
          <p className="template-layout-hint">Preview auto-arranged overlapping fields.</p>
        ) : null}
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
        <div
          className="template-scope-preview template-entry-preview"
          aria-label="Entry preview"
          style={getTemplateLayoutGridStyle(entryPreviewFields)}
        >
          {entryPreviewFields.length === 0 ? (
            <span className="template-preview-empty">Plain note text</span>
          ) : (
            entryPreviewFields.map((field) => (
              <span
                className="template-preview-chip"
                data-layout-adjusted={field.layoutWasAdjusted}
                data-field-type={field.type}
                key={field.clientId}
                style={getEditableTemplateFieldGridStyle(field)}
              >
                {field.name}
                <small>
                  {field.type} - R{field.layoutRow} C{field.layoutColumn}
                </small>
              </span>
            ))
          )}
        </div>
        {entryLayoutAdjusted ? (
          <p className="template-layout-hint">Preview auto-arranged overlapping fields.</p>
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

