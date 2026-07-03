import { type FormEvent, useEffect, useRef, useState } from 'react';
import { Icon } from '../../components/Icon';
import { type Translate } from '../../localization';
import {
  getContextChipStyle,
  getTaskCardStyle,
} from '../../taskUtils';
import type {
  ProjectResponse,
  TaskItemDetailResponse,
  TaskTemplateDetailResponse,
} from '../../types';
import { type CreateTaskItemOptions } from './taskWallTypes';

interface DraftTaskCardProps {
  onCancel: () => void;
  onCreateTaskItem: (
    title: string,
    options?: CreateTaskItemOptions,
  ) => Promise<TaskItemDetailResponse | null>;
  onCreated: (taskItem: TaskItemDetailResponse) => void;
  projects: ProjectResponse[];
  selectedProjectId: string;
  t: Translate;
  templates: TaskTemplateDetailResponse[];
  workspaceColor: string | null;
  workspaceId: string;
  workspaceName: string;
}

export function DraftTaskCard({
  onCancel,
  onCreateTaskItem,
  onCreated,
  projects,
  selectedProjectId,
  t,
  templates,
  workspaceColor,
  workspaceId,
  workspaceName,
}: DraftTaskCardProps) {
  const [title, setTitle] = useState('');
  const [selectedTemplateId, setSelectedTemplateId] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  const selectedProject = projects.find((project) => project.id === selectedProjectId) ?? null;

  useEffect(() => {
    setSelectedTemplateId((currentId) =>
      currentId && templates.some((template) => template.id === currentId)
        ? currentId
        : templates[0]?.id ?? '',
    );
  }, [templates]);

  useEffect(() => {
    inputRef.current?.focus();
  }, []);

  const submitDraft = async () => {
    const trimmedTitle = title.trim();
    if (!trimmedTitle || isSubmitting) {
      inputRef.current?.focus();
      return;
    }

    setIsSubmitting(true);
    const created = await onCreateTaskItem(trimmedTitle, {
      workspaceId,
      projectId: selectedProject?.id ?? null,
      category: selectedProject?.name ?? null,
      taskTemplateId: selectedTemplateId || null,
    });
    setIsSubmitting(false);

    if (created) {
      onCreated(created);
    }
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    await submitDraft();
  };

  return (
    <article
      className="task-card task-card-draft"
      data-expanded="true"
      data-state="active"
      style={getTaskCardStyle(workspaceColor ?? '#FFF3A6')}
    >
      <div className="task-card-detail">
        <section className="task-detail draft-task-detail" aria-label={t('newTask')}>
          <form
            className="detail-header task-detail-header draft-task-header"
            onSubmit={(event) => void handleSubmit(event)}
          >
            <button
              className="icon-button task-detail-back-button"
              onClick={onCancel}
              title={t('backToWall')}
              type="button"
            >
              <Icon name="back" />
              <span className="sr-only">{t('backToWall')}</span>
            </button>
            <div className="task-header-editor">
              <p className="detail-kicker">
                {workspaceName} / {t('newTask')}
              </p>
              <div className="task-title-row">
                <input
                  aria-label={t('taskTitleRequired')}
                  className="task-title-input"
                  onChange={(event) => setTitle(event.target.value)}
                  onKeyDown={(event) => {
                    if (event.key === 'Enter') {
                      event.preventDefault();
                      void submitDraft();
                    }

                    if (event.key === 'Escape' && !title.trim()) {
                      onCancel();
                    }
                  }}
                  placeholder={t('newTaskTitlePlaceholder')}
                  ref={inputRef}
                  required
                  type="text"
                  value={title}
                />
              </div>
              <div className="task-header-fields task-header-fields-edit draft-task-controls">
                {selectedProject ? (
                  <span className="task-meta-chip draft-meta-chip" style={getContextChipStyle(selectedProject.color)}>
                    <Icon name="tag" />
                    {t('category')}: {selectedProject.name}
                  </span>
                ) : (
                  <span className="task-meta-chip draft-meta-chip">
                    <Icon name="tag" />
                    {t('category')}: {t('noCategory')}
                  </span>
                )}
                {templates.length > 0 ? (
                  <label className="task-meta-chip draft-template-chip">
                    <Icon name="templates" />
                    <span className="sr-only">{t('templates')}</span>
                    <select
                      aria-label={t('templates')}
                      onChange={(event) => setSelectedTemplateId(event.target.value)}
                      value={selectedTemplateId}
                    >
                      {templates.map((template) => (
                        <option key={template.id} value={template.id}>
                          {template.name}
                        </option>
                      ))}
                    </select>
                  </label>
                ) : null}
              </div>
            </div>
            <div className="detail-actions">
              <button
                className="secondary-action"
                disabled={!title.trim() || isSubmitting}
                type="submit"
              >
                <Icon name="plus" />
                <span>{t('addTask')}</span>
              </button>
            </div>
          </form>
          <section className="timeline-panel draft-notes-placeholder">
            <h3>{t('notes')}</h3>
            <p>{t('draftTaskHelp')}</p>
          </section>
        </section>
      </div>
    </article>
  );
}
