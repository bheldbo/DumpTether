import { useEffect, useState } from 'react';
import { Icon } from '../../components/Icon';
import { type Translate } from '../../localization';
import { TemplateEditor } from './TemplateEditor';
import type {
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

