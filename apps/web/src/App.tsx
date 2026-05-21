import { FormEvent, useCallback, useEffect, useMemo, useState } from 'react';
import {
  addTaskTimelineEntry,
  archiveTaskItem,
  createTaskItem,
  createTaskTemplate,
  deleteTaskTemplate,
  getTaskItem,
  getTaskTemplate,
  listArchiveResolutions,
  listTaskItems,
  listTaskTemplates,
  reopenTaskItem,
  updateTaskItem,
  updateTaskTemplate,
} from './api';
import './App.css';
import { FieldEditorList, FieldValueList } from './fieldRenderers';
import { toFieldValueMap } from './fieldValues';
import type {
  ArchiveResolutionResponse,
  ArchiveTaskItemRequest,
  FieldDefinitionType,
  FieldValueMap,
  TaskItemDetailResponse,
  TaskItemSummaryResponse,
  TaskTemplateDetailResponse,
  UpsertFieldDefinitionRequest,
} from './types';

type ViewId =
  | 'inbox'
  | 'active'
  | 'waiting'
  | 'stale'
  | 'archive'
  | 'templates';

interface ViewDefinition {
  id: ViewId;
  label: string;
  icon: IconName;
}

type IconName =
  | 'archive'
  | 'clock'
  | 'inbox'
  | 'list'
  | 'note'
  | 'plus'
  | 'refresh'
  | 'templates'
  | 'trash'
  | 'waiting';

interface EditableTemplateField {
  clientId: string;
  id?: string;
  name: string;
  type: FieldDefinitionType;
  required: boolean;
  sortOrder: number;
  optionsText: string;
}

const viewDefinitions: ViewDefinition[] = [
  { id: 'inbox', label: 'Inbox', icon: 'inbox' },
  { id: 'active', label: 'All active', icon: 'list' },
  { id: 'waiting', label: 'Waiting', icon: 'waiting' },
  { id: 'stale', label: 'Not touched', icon: 'clock' },
  { id: 'archive', label: 'Archive', icon: 'archive' },
  { id: 'templates', label: 'Templates', icon: 'templates' },
];

const fieldTypes: FieldDefinitionType[] = [
  'Text',
  'LongText',
  'Date',
  'Checkbox',
  'Select',
];
const staleAfterDays = 7;

function App() {
  const [activeTaskItems, setActiveTaskItems] = useState<TaskItemSummaryResponse[]>([]);
  const [archivedTaskItems, setArchivedTaskItems] = useState<TaskItemSummaryResponse[]>([]);
  const [archiveResolutions, setArchiveResolutions] = useState<ArchiveResolutionResponse[]>([]);
  const [templates, setTemplates] = useState<TaskTemplateDetailResponse[]>([]);
  const [currentView, setCurrentView] = useState<ViewId>(getInitialView);
  const [selectedTaskId, setSelectedTaskId] = useState<string | null>(null);
  const [selectedTask, setSelectedTask] = useState<TaskItemDetailResponse | null>(null);
  const [isArchiveDialogOpen, setIsArchiveDialogOpen] = useState(false);
  const [isLoadingWorkspace, setIsLoadingWorkspace] = useState(true);
  const [isLoadingDetail, setIsLoadingDetail] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const loadWorkspace = useCallback(async () => {
    setIsLoadingWorkspace(true);

    try {
      const [active, archive, resolutions, templateSummaries] = await Promise.all([
        listTaskItems('Active'),
        listTaskItems('Archive'),
        listArchiveResolutions(),
        listTaskTemplates(),
      ]);
      const templateDetails = await Promise.all(
        templateSummaries.map((template) => getTaskTemplate(template.id)),
      );

      setActiveTaskItems(active);
      setArchivedTaskItems(archive);
      setArchiveResolutions(resolutions);
      setTemplates(templateDetails);
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    } finally {
      setIsLoadingWorkspace(false);
    }
  }, []);

  useEffect(() => {
    void loadWorkspace();
  }, [loadWorkspace]);

  const visibleTaskItems = useMemo(() => {
    return getVisibleTaskItems(currentView, activeTaskItems, archivedTaskItems);
  }, [activeTaskItems, archivedTaskItems, currentView]);

  const counts = useMemo(() => {
    return {
      inbox: getVisibleTaskItems('inbox', activeTaskItems, archivedTaskItems).length,
      active: activeTaskItems.length,
      waiting: getVisibleTaskItems('waiting', activeTaskItems, archivedTaskItems).length,
      stale: getVisibleTaskItems('stale', activeTaskItems, archivedTaskItems).length,
      archive: archivedTaskItems.length,
      templates: templates.length,
    } satisfies Record<ViewId, number>;
  }, [activeTaskItems, archivedTaskItems, templates.length]);

  useEffect(() => {
    if (currentView === 'templates') {
      return;
    }

    if (visibleTaskItems.length === 0) {
      setSelectedTaskId(null);
      return;
    }

    const selectedTaskIsVisible = visibleTaskItems.some(
      (taskItem) => taskItem.id === selectedTaskId,
    );

    if (!selectedTaskIsVisible) {
      setSelectedTaskId(visibleTaskItems[0].id);
    }
  }, [currentView, selectedTaskId, visibleTaskItems]);

  useEffect(() => {
    if (!selectedTaskId || currentView === 'templates') {
      setSelectedTask(null);
      return;
    }

    let isStaleRequest = false;
    setIsLoadingDetail(true);

    getTaskItem(selectedTaskId)
      .then((taskItem) => {
        if (!isStaleRequest) {
          setSelectedTask(taskItem);
          setErrorMessage(null);
        }
      })
      .catch((error) => {
        if (!isStaleRequest) {
          setErrorMessage(getErrorMessage(error));
        }
      })
      .finally(() => {
        if (!isStaleRequest) {
          setIsLoadingDetail(false);
        }
      });

    return () => {
      isStaleRequest = true;
    };
  }, [currentView, selectedTaskId]);

  const handleCreateTaskItem = async (
    title: string,
    taskTemplateId: string | null,
    fieldValues: FieldValueMap,
  ) => {
    try {
      const created = await createTaskItem({
        title,
        taskTemplateId,
        fieldValues,
      });
      setCurrentView('inbox');
      setSelectedTaskId(created.id);
      setSelectedTask(created);
      await loadWorkspace();
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  const handleUpdateFieldValues = async (fieldValues: FieldValueMap) => {
    if (!selectedTask) {
      return;
    }

    try {
      const updated = await updateTaskItem(selectedTask.id, { fieldValues });
      setSelectedTask(updated);
      await loadWorkspace();
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  const handleAddTimelineEntry = async (note: string) => {
    if (!selectedTask) {
      return;
    }

    try {
      const updated = await addTaskTimelineEntry(selectedTask.id, { note });
      setSelectedTask(updated);
      await loadWorkspace();
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  const handleArchiveTaskItem = async (requestBody: ArchiveTaskItemRequest) => {
    if (!selectedTask) {
      return;
    }

    try {
      const archived = await archiveTaskItem(selectedTask.id, requestBody);
      setCurrentView('archive');
      setSelectedTaskId(archived.id);
      setSelectedTask(archived);
      setIsArchiveDialogOpen(false);
      await loadWorkspace();
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  const handleReopenTaskItem = async (note?: string) => {
    if (!selectedTask) {
      return;
    }

    try {
      const reopened = await reopenTaskItem(selectedTask.id, { note });
      setCurrentView('active');
      setSelectedTaskId(reopened.id);
      setSelectedTask(reopened);
      await loadWorkspace();
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  const handleSaveTemplate = async (
    id: string | null,
    name: string,
    fields: UpsertFieldDefinitionRequest[],
  ) => {
    try {
      if (id) {
        await updateTaskTemplate(id, { name, fields });
      } else {
        await createTaskTemplate({ name, fields });
      }

      await loadWorkspace();
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  const handleDeleteTemplate = async (id: string) => {
    try {
      await deleteTaskTemplate(id);
      await loadWorkspace();
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  const handleSelectView = (viewId: ViewId) => {
    setCurrentView(viewId);

    const url = new URL(window.location.href);
    url.searchParams.set('view', viewId);
    window.history.replaceState(null, '', url);
  };

  return (
    <main className="app-shell">
      <Sidebar
        counts={counts}
        currentView={currentView}
        onRefresh={loadWorkspace}
        onSelectView={handleSelectView}
      />

      <section className="workspace" aria-label="Task workspace">
        {errorMessage ? (
          <div className="error-banner" role="alert">
            <strong>Something needs attention.</strong>
            <span>{errorMessage}</span>
          </div>
        ) : null}

        {currentView === 'templates' ? (
          <TemplatesPage
            isLoading={isLoadingWorkspace}
            onDeleteTemplate={handleDeleteTemplate}
            onSaveTemplate={handleSaveTemplate}
            templates={templates}
          />
        ) : (
          <>
            <TaskList
              currentView={currentView}
              isLoading={isLoadingWorkspace}
              onCreateTaskItem={handleCreateTaskItem}
              onSelectTaskItem={setSelectedTaskId}
              selectedTaskId={selectedTaskId}
              taskItems={visibleTaskItems}
              templates={templates}
            />

            <TaskDetail
              archiveResolutions={archiveResolutions}
              isArchiveDialogOpen={isArchiveDialogOpen}
              isLoading={isLoadingDetail}
              onAddTimelineEntry={handleAddTimelineEntry}
              onArchive={handleArchiveTaskItem}
              onCloseArchiveDialog={() => setIsArchiveDialogOpen(false)}
              onOpenArchiveDialog={() => setIsArchiveDialogOpen(true)}
              onReopen={handleReopenTaskItem}
              onUpdateFieldValues={handleUpdateFieldValues}
              taskItem={selectedTask}
            />
          </>
        )}
      </section>
    </main>
  );
}

function Sidebar({
  counts,
  currentView,
  onRefresh,
  onSelectView,
}: {
  counts: Record<ViewId, number>;
  currentView: ViewId;
  onRefresh: () => void;
  onSelectView: (viewId: ViewId) => void;
}) {
  return (
    <aside className="sidebar" aria-label="DumpTether navigation">
      <div className="brand">
        <div className="brand-mark">DT</div>
        <div>
          <p className="brand-name">DumpTether</p>
          <p className="brand-subtitle">Personal task evidence</p>
        </div>
      </div>

      <nav className="view-nav" aria-label="Task views">
        {viewDefinitions.map((view) => (
          <button
            aria-current={currentView === view.id ? 'page' : undefined}
            className="nav-item"
            key={view.id}
            onClick={() => onSelectView(view.id)}
            type="button"
          >
            <Icon name={view.icon} />
            <span>{view.label}</span>
            <span className="nav-count">{counts[view.id]}</span>
          </button>
        ))}
      </nav>

      <button className="refresh-button" onClick={onRefresh} type="button">
        <Icon name="refresh" />
        <span>Refresh</span>
      </button>
    </aside>
  );
}

function TaskList({
  currentView,
  isLoading,
  onCreateTaskItem,
  onSelectTaskItem,
  selectedTaskId,
  taskItems,
  templates,
}: {
  currentView: ViewId;
  isLoading: boolean;
  onCreateTaskItem: (
    title: string,
    taskTemplateId: string | null,
    fieldValues: FieldValueMap,
  ) => Promise<void>;
  onSelectTaskItem: (id: string) => void;
  selectedTaskId: string | null;
  taskItems: TaskItemSummaryResponse[];
  templates: TaskTemplateDetailResponse[];
}) {
  return (
    <section className="task-list" aria-labelledby="task-list-title">
      <div className="list-header">
        <div>
          <h1 id="task-list-title">{getViewLabel(currentView)}</h1>
          <p>{getViewDescription(currentView)}</p>
        </div>
      </div>

      {currentView !== 'archive' ? (
        <CreateTaskForm
          onCreateTaskItem={onCreateTaskItem}
          templates={templates}
        />
      ) : null}

      <div className="list-body" aria-busy={isLoading}>
        {isLoading ? <p className="empty-copy">Loading tasks...</p> : null}

        {!isLoading && taskItems.length === 0 ? (
          <p className="empty-copy">Nothing in this view yet.</p>
        ) : null}

        {taskItems.map((taskItem) => (
          <button
            className="task-row"
            data-selected={selectedTaskId === taskItem.id}
            key={taskItem.id}
            onClick={() => onSelectTaskItem(taskItem.id)}
            type="button"
          >
            <span className="task-row-title">{taskItem.title}</span>
            <span className="task-row-meta">
              {taskItem.status ?? 'No status'} - touched{' '}
              {formatRelativeDate(taskItem.lastTouchedAt)}
            </span>
            {taskItem.followUpAt ? (
              <span className="task-row-follow-up">
                Follow up {formatShortDate(taskItem.followUpAt)}
              </span>
            ) : null}
          </button>
        ))}
      </div>
    </section>
  );
}

function CreateTaskForm({
  onCreateTaskItem,
  templates,
}: {
  onCreateTaskItem: (
    title: string,
    taskTemplateId: string | null,
    fieldValues: FieldValueMap,
  ) => Promise<void>;
  templates: TaskTemplateDetailResponse[];
}) {
  const [title, setTitle] = useState('');
  const [taskTemplateId, setTaskTemplateId] = useState<string | null>(null);
  const [fieldValues, setFieldValues] = useState<FieldValueMap>({});
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (templates.length === 0) {
      setTaskTemplateId(null);
      return;
    }

    setTaskTemplateId((currentTemplateId) =>
      currentTemplateId && templates.some((template) => template.id === currentTemplateId)
        ? currentTemplateId
        : templates[0].id,
    );
  }, [templates]);

  const selectedTemplate = templates.find((template) => template.id === taskTemplateId) ?? null;

  useEffect(() => {
    setFieldValues({});
  }, [taskTemplateId]);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const trimmedTitle = title.trim();
    if (!trimmedTitle) {
      return;
    }

    setIsSubmitting(true);
    await onCreateTaskItem(
      trimmedTitle,
      selectedTemplate?.id ?? null,
      selectedTemplate ? withDefaultFieldValues(selectedTemplate, fieldValues) : {},
    );
    setTitle('');
    setFieldValues({});
    setIsSubmitting(false);
  };

  return (
    <form className="create-task-form" onSubmit={handleSubmit}>
      <input
        aria-label="New task title"
        onChange={(event) => setTitle(event.target.value)}
        placeholder="Capture a task..."
        type="text"
        value={title}
      />

      <select
        aria-label="Task template"
        onChange={(event) => setTaskTemplateId(event.target.value || null)}
        value={taskTemplateId ?? ''}
      >
        {templates.length === 0 ? <option value="">No templates</option> : null}
        {templates.map((template) => (
          <option key={template.id} value={template.id}>
            {template.name}
          </option>
        ))}
      </select>

      {selectedTemplate && selectedTemplate.fields.length > 0 ? (
        <div className="create-fields">
          <FieldEditorList
            fields={selectedTemplate.fields}
            onChange={(fieldId, value) =>
              setFieldValues((currentValues) => ({
                ...currentValues,
                [fieldId]: value,
              }))
            }
            values={fieldValues}
          />
        </div>
      ) : null}

      <button disabled={!title.trim() || isSubmitting} type="submit">
        <Icon name="plus" />
        <span>Add</span>
      </button>
    </form>
  );
}

function TaskDetail({
  archiveResolutions,
  isArchiveDialogOpen,
  isLoading,
  onAddTimelineEntry,
  onArchive,
  onCloseArchiveDialog,
  onOpenArchiveDialog,
  onReopen,
  onUpdateFieldValues,
  taskItem,
}: {
  archiveResolutions: ArchiveResolutionResponse[];
  isArchiveDialogOpen: boolean;
  isLoading: boolean;
  onAddTimelineEntry: (note: string) => Promise<void>;
  onArchive: (requestBody: ArchiveTaskItemRequest) => Promise<void>;
  onCloseArchiveDialog: () => void;
  onOpenArchiveDialog: () => void;
  onReopen: (note?: string) => Promise<void>;
  onUpdateFieldValues: (fieldValues: FieldValueMap) => Promise<void>;
  taskItem: TaskItemDetailResponse | null;
}) {
  const [reopenNote, setReopenNote] = useState('');
  const [fieldDraft, setFieldDraft] = useState<FieldValueMap>({});
  const [isSavingFields, setIsSavingFields] = useState(false);

  useEffect(() => {
    setReopenNote('');
    setFieldDraft(taskItem ? toFieldValueMap(taskItem.fieldValues) : {});
  }, [taskItem]);

  if (!taskItem) {
    return (
      <section className="task-detail empty-detail" aria-label="Task detail">
        <p>Select a task to see its structured fields and timeline.</p>
      </section>
    );
  }

  const fieldValuesCanBeEdited = !taskItem.archivedAt && Boolean(taskItem.template);

  return (
    <section className="task-detail" aria-busy={isLoading} aria-label="Task detail">
      <div className="detail-header">
        <div>
          <p className="detail-kicker">
            {taskItem.archivedAt ? 'Archived task' : 'Active task'}
          </p>
          <h2>{taskItem.title}</h2>
        </div>

        {taskItem.archivedAt ? (
          <form
            className="reopen-form"
            onSubmit={(event) => {
              event.preventDefault();
              void onReopen(reopenNote.trim() || undefined);
            }}
          >
            <input
              aria-label="Reopen note"
              onChange={(event) => setReopenNote(event.target.value)}
              placeholder="Optional reopen note"
              type="text"
              value={reopenNote}
            />
            <button type="submit">Reopen</button>
          </form>
        ) : (
          <button className="secondary-action" onClick={onOpenArchiveDialog} type="button">
            <Icon name="archive" />
            <span>Archive</span>
          </button>
        )}
      </div>

      <div className="detail-meta">
        <MetaItem label="Template" value={taskItem.template?.name ?? 'None'} />
        <MetaItem label="Status" value={taskItem.status ?? 'No status'} />
        <MetaItem label="Created" value={formatDateTime(taskItem.createdAt)} />
        <MetaItem label="Touched" value={formatDateTime(taskItem.lastTouchedAt)} />
        <MetaItem
          label="Follow-up"
          value={taskItem.followUpAt ? formatDateTime(taskItem.followUpAt) : 'None'}
        />
      </div>

      <section className="detail-section" aria-labelledby="fields-title">
        <div className="section-heading">
          <h3 id="fields-title">Structured fields</h3>
          {fieldValuesCanBeEdited ? (
            <button
              disabled={isSavingFields}
              onClick={async () => {
                setIsSavingFields(true);
                await onUpdateFieldValues(
                  withDefaultFieldValues(taskItem.template!, fieldDraft),
                );
                setIsSavingFields(false);
              }}
              type="button"
            >
              Save fields
            </button>
          ) : null}
        </div>

        {fieldValuesCanBeEdited ? (
          <FieldEditorList
            fields={taskItem.template!.fields}
            onChange={(fieldId, value) =>
              setFieldDraft((currentValues) => ({
                ...currentValues,
                [fieldId]: value,
              }))
            }
            values={fieldDraft}
          />
        ) : (
          <FieldValueList
            fieldValues={taskItem.fieldValues}
            template={taskItem.template}
          />
        )}
      </section>

      <TimelinePanel
        onAddTimelineEntry={onAddTimelineEntry}
        timelineEntries={taskItem.timelineEntries}
      />

      {isArchiveDialogOpen ? (
        <ArchiveDialog
          archiveResolutions={archiveResolutions}
          onArchive={onArchive}
          onClose={onCloseArchiveDialog}
          taskTitle={taskItem.title}
        />
      ) : null}
    </section>
  );
}

function TemplatesPage({
  isLoading,
  onDeleteTemplate,
  onSaveTemplate,
  templates,
}: {
  isLoading: boolean;
  onDeleteTemplate: (id: string) => Promise<void>;
  onSaveTemplate: (
    id: string | null,
    name: string,
    fields: UpsertFieldDefinitionRequest[],
  ) => Promise<void>;
  templates: TaskTemplateDetailResponse[];
}) {
  const [selectedTemplateId, setSelectedTemplateId] = useState<string | null>(null);
  const selectedTemplate =
    templates.find((template) => template.id === selectedTemplateId) ?? null;

  useEffect(() => {
    if (selectedTemplateId && templates.some((template) => template.id === selectedTemplateId)) {
      return;
    }

    setSelectedTemplateId(templates[0]?.id ?? null);
  }, [selectedTemplateId, templates]);

  return (
    <section className="templates-page" aria-labelledby="templates-title">
      <div className="templates-list">
        <div className="list-header">
          <div>
            <h1 id="templates-title">Templates</h1>
            <p>Define the structured shape tasks can use.</p>
          </div>
          <button onClick={() => setSelectedTemplateId(null)} type="button">
            <Icon name="plus" />
            <span>New</span>
          </button>
        </div>

        <div className="list-body" aria-busy={isLoading}>
          {templates.map((template) => (
            <button
              className="task-row"
              data-selected={selectedTemplateId === template.id}
              key={template.id}
              onClick={() => setSelectedTemplateId(template.id)}
              type="button"
            >
              <span className="task-row-title">{template.name}</span>
              <span className="task-row-meta">{template.fields.length} fields</span>
            </button>
          ))}
        </div>
      </div>

      <TemplateEditor
        key={selectedTemplate?.id ?? 'new-template'}
        onDeleteTemplate={onDeleteTemplate}
        onSaveTemplate={onSaveTemplate}
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
  ) => Promise<void>;
  template: TaskTemplateDetailResponse | null;
}) {
  const [name, setName] = useState(template?.name ?? '');
  const [fields, setFields] = useState<EditableTemplateField[]>(
    () => template?.fields.map(toEditableTemplateField) ?? [],
  );
  const [isSubmitting, setIsSubmitting] = useState(false);

  const addField = () => {
    setFields((currentFields) => [
      ...currentFields,
      {
        clientId: crypto.randomUUID(),
        name: 'New field',
        type: 'Text',
        required: false,
        sortOrder: currentFields.length,
        optionsText: '',
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
      const index = currentFields.findIndex((field) => field.clientId === clientId);
      const nextIndex = index + direction;

      if (index < 0 || nextIndex < 0 || nextIndex >= currentFields.length) {
        return currentFields;
      }

      const reorderedFields = [...currentFields];
      const [field] = reorderedFields.splice(index, 1);
      reorderedFields.splice(nextIndex, 0, field);

      return reorderedFields.map((candidate, sortOrder) => ({
        ...candidate,
        sortOrder,
      }));
    });
  };

  const removeField = (clientId: string) => {
    setFields((currentFields) =>
      currentFields
        .filter((field) => field.clientId !== clientId)
        .map((field, sortOrder) => ({ ...field, sortOrder })),
    );
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const trimmedName = name.trim();
    if (!trimmedName) {
      return;
    }

    setIsSubmitting(true);
    await onSaveTemplate(
      template?.id ?? null,
      trimmedName,
      fields.map((field, index) => ({
        id: field.id ?? null,
        name: field.name.trim(),
        type: field.type,
        required: field.required,
        sortOrder: index,
        options: field.type === 'Select' ? splitOptions(field.optionsText) : [],
      })),
    );
    setIsSubmitting(false);
  };

  return (
    <form className="template-editor" onSubmit={handleSubmit}>
      <div className="detail-header">
        <div>
          <p className="detail-kicker">{template ? 'Edit template' : 'New template'}</p>
          <h2>{template?.name ?? 'Template'}</h2>
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
          onChange={(event) => setName(event.target.value)}
          required
          type="text"
          value={name}
        />
      </label>

      <div className="section-heading">
        <h3>Fields</h3>
        <button onClick={addField} type="button">
          <Icon name="plus" />
          <span>Add field</span>
        </button>
      </div>

      <div className="template-fields">
        {fields.length === 0 ? (
          <p className="empty-copy">No fields yet.</p>
        ) : null}

        {fields.map((field, index) => (
          <div className="template-field-row" key={field.clientId}>
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
              onChange={(event) =>
                updateField(field.clientId, {
                  type: event.target.value as FieldDefinitionType,
                })
              }
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

            <div className="field-order-actions">
              <button
                disabled={index === 0}
                onClick={() => moveField(field.clientId, -1)}
                type="button"
              >
                Up
              </button>
              <button
                disabled={index === fields.length - 1}
                onClick={() => moveField(field.clientId, 1)}
                type="button"
              >
                Down
              </button>
              <button
                className="ghost-button"
                onClick={() => removeField(field.clientId)}
                type="button"
              >
                Remove
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
        ))}
      </div>

      <div className="dialog-actions">
        <button disabled={!name.trim() || isSubmitting} type="submit">
          Save template
        </button>
      </div>
    </form>
  );
}

function MetaItem({ label, value }: { label: string; value: string }) {
  return (
    <div className="meta-item">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function TimelinePanel({
  onAddTimelineEntry,
  timelineEntries,
}: {
  onAddTimelineEntry: (note: string) => Promise<void>;
  timelineEntries: TaskItemDetailResponse['timelineEntries'];
}) {
  return (
    <section className="timeline-panel" aria-labelledby="timeline-title">
      <div className="section-heading">
        <h3 id="timeline-title">Timeline</h3>
        <span>{timelineEntries.length} entries</span>
      </div>

      <AddTimelineEntryForm onAddTimelineEntry={onAddTimelineEntry} />

      <ol className="timeline-list">
        {timelineEntries.map((entry) => (
          <li className="timeline-entry" key={entry.id}>
            <div className="timeline-dot" aria-hidden="true" />
            <div>
              <div className="timeline-entry-header">
                <strong>{entry.summary}</strong>
                <time dateTime={entry.occurredAt}>{formatDateTime(entry.occurredAt)}</time>
              </div>
              {entry.details ? <p>{entry.details}</p> : null}
              <span className="timeline-kind">{entry.kind}</span>
            </div>
          </li>
        ))}
      </ol>
    </section>
  );
}

function AddTimelineEntryForm({
  onAddTimelineEntry,
}: {
  onAddTimelineEntry: (note: string) => Promise<void>;
}) {
  const [note, setNote] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const trimmedNote = note.trim();
    if (!trimmedNote) {
      return;
    }

    setIsSubmitting(true);
    await onAddTimelineEntry(trimmedNote);
    setNote('');
    setIsSubmitting(false);
  };

  return (
    <form className="timeline-form" onSubmit={handleSubmit}>
      <textarea
        aria-label="Timeline note"
        onChange={(event) => setNote(event.target.value)}
        placeholder="Add evidence, a status note, or a source detail..."
        rows={3}
        value={note}
      />
      <button disabled={!note.trim() || isSubmitting} type="submit">
        <Icon name="note" />
        <span>Add note</span>
      </button>
    </form>
  );
}

function ArchiveDialog({
  archiveResolutions,
  onArchive,
  onClose,
  taskTitle,
}: {
  archiveResolutions: ArchiveResolutionResponse[];
  onArchive: (requestBody: ArchiveTaskItemRequest) => Promise<void>;
  onClose: () => void;
  taskTitle: string;
}) {
  const [archiveResolutionId, setArchiveResolutionId] = useState(
    archiveResolutions[0]?.id ?? '',
  );
  const [note, setNote] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  const selectedResolution = archiveResolutions.find(
    (resolution) => resolution.id === archiveResolutionId,
  );
  const noteIsRequired = selectedResolution?.requiresExplanation ?? false;
  const canSubmit = Boolean(archiveResolutionId) && (!noteIsRequired || note.trim());

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    if (!canSubmit) {
      return;
    }

    setIsSubmitting(true);
    await onArchive({
      archiveResolutionId,
      note: note.trim() || null,
    });
    setIsSubmitting(false);
  };

  return (
    <div className="dialog-backdrop" role="presentation">
      <section
        aria-labelledby="archive-dialog-title"
        aria-modal="true"
        className="archive-dialog"
        role="dialog"
      >
        <div className="dialog-header">
          <div>
            <p className="detail-kicker">Archive task</p>
            <h2 id="archive-dialog-title">{taskTitle}</h2>
          </div>
          <button className="icon-button" onClick={onClose} type="button">
            <span aria-hidden="true">x</span>
            <span className="sr-only">Close archive dialog</span>
          </button>
        </div>

        <form className="archive-form" onSubmit={handleSubmit}>
          <label>
            Resolution reason
            <select
              onChange={(event) => setArchiveResolutionId(event.target.value)}
              required
              value={archiveResolutionId}
            >
              {archiveResolutions.length === 0 ? (
                <option value="">No archive reasons available</option>
              ) : null}
              {archiveResolutions.map((resolution) => (
                <option key={resolution.id} value={resolution.id}>
                  {resolution.name}
                </option>
              ))}
            </select>
          </label>

          {selectedResolution?.description ? (
            <p className="resolution-description">{selectedResolution.description}</p>
          ) : null}

          <label>
            Archive note {noteIsRequired ? '(required)' : '(optional)'}
            <textarea
              onChange={(event) => setNote(event.target.value)}
              required={noteIsRequired}
              rows={4}
              value={note}
            />
          </label>

          <div className="dialog-actions">
            <button className="ghost-button" onClick={onClose} type="button">
              Cancel
            </button>
            <button disabled={!canSubmit || isSubmitting} type="submit">
              Archive
            </button>
          </div>
        </form>
      </section>
    </div>
  );
}

function Icon({ name }: { name: IconName }) {
  const paths: Record<IconName, string> = {
    archive: 'M4 7h16v13H4V7Zm2-4h12l2 4H4l2-4Zm5 8h2',
    clock: 'M12 4a8 8 0 1 0 0 16 8 8 0 0 0 0-16Zm0 4v5l3 2',
    inbox: 'M4 5h16v10l-3 4H7l-3-4V5Zm0 10h5l1.5 2h3L15 15h5',
    list: 'M8 6h12M8 12h12M8 18h12M4 6h.01M4 12h.01M4 18h.01',
    note: 'M5 4h11l3 3v13H5V4Zm11 0v4h4M8 12h8M8 16h6',
    plus: 'M12 5v14M5 12h14',
    refresh: 'M20 7v5h-5M4 17v-5h5M18 10a6 6 0 0 0-10-4L4 10m2 4a6 6 0 0 0 10 4l4-4',
    templates: 'M4 5h7v7H4V5Zm9 0h7v7h-7V5ZM4 14h7v5H4v-5Zm9 0h7v5h-7v-5Z',
    trash: 'M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3',
    waiting: 'M6 4h12M8 4v5l4 3 4-3V4M8 20v-5l4-3 4 3v5M6 20h12',
  };

  return (
    <svg
      aria-hidden="true"
      className="icon"
      fill="none"
      focusable="false"
      viewBox="0 0 24 24"
    >
      <path d={paths[name]} stroke="currentColor" strokeLinecap="round" strokeLinejoin="round" strokeWidth="1.8" />
    </svg>
  );
}

function getVisibleTaskItems(
  viewId: ViewId,
  activeTaskItems: TaskItemSummaryResponse[],
  archivedTaskItems: TaskItemSummaryResponse[],
) {
  switch (viewId) {
    case 'archive':
      return archivedTaskItems;
    case 'waiting':
      return activeTaskItems.filter((taskItem) =>
        taskItem.status?.toLowerCase().includes('waiting'),
      );
    case 'stale':
      return activeTaskItems.filter((taskItem) => {
        const touchedAt = new Date(taskItem.lastTouchedAt).getTime();
        const staleAt = Date.now() - staleAfterDays * 24 * 60 * 60 * 1000;
        return touchedAt < staleAt;
      });
    case 'inbox':
      return activeTaskItems.filter((taskItem) => !taskItem.status);
    case 'active':
    case 'templates':
    default:
      return activeTaskItems;
  }
}

function getViewLabel(viewId: ViewId) {
  return viewDefinitions.find((view) => view.id === viewId)?.label ?? 'Tasks';
}

function getViewDescription(viewId: ViewId) {
  switch (viewId) {
    case 'archive':
      return 'Closed tasks stay visible with their evidence trail.';
    case 'waiting':
      return 'Active tasks currently marked as waiting.';
    case 'stale':
      return `Active tasks not touched in ${staleAfterDays} days.`;
    case 'active':
      return 'Every active task in the development workspace.';
    case 'templates':
      return 'Template definitions for task structure.';
    case 'inbox':
    default:
      return 'Fresh captures without a status yet.';
  }
}

function getInitialView(): ViewId {
  const view = new URL(window.location.href).searchParams.get('view');

  return viewDefinitions.some((definition) => definition.id === view)
    ? (view as ViewId)
    : 'inbox';
}

function withDefaultFieldValues(
  template: TaskTemplateDetailResponse,
  values: FieldValueMap,
): FieldValueMap {
  return Object.fromEntries(
    template.fields.map((field) => [
      field.id,
      values[field.id] ?? (field.type === 'Checkbox' ? false : null),
    ]),
  );
}

function toEditableTemplateField(
  field: TaskTemplateDetailResponse['fields'][number],
): EditableTemplateField {
  return {
    clientId: field.id,
    id: field.id,
    name: field.name,
    type: field.type,
    required: field.required,
    sortOrder: field.sortOrder,
    optionsText: field.options.join('\n'),
  };
}

function splitOptions(optionsText: string) {
  return optionsText
    .split(/\r?\n/)
    .map((option) => option.trim())
    .filter(Boolean);
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value));
}

function formatShortDate(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    month: 'short',
    day: 'numeric',
  }).format(new Date(value));
}

function formatRelativeDate(value: string) {
  const elapsedMs = Date.now() - new Date(value).getTime();
  const elapsedMinutes = Math.max(1, Math.round(elapsedMs / 60000));

  if (elapsedMinutes < 60) {
    return `${elapsedMinutes}m ago`;
  }

  const elapsedHours = Math.round(elapsedMinutes / 60);
  if (elapsedHours < 24) {
    return `${elapsedHours}h ago`;
  }

  return `${Math.round(elapsedHours / 24)}d ago`;
}

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : 'Unexpected error.';
}

export default App;
