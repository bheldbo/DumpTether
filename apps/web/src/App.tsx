import { FormEvent, useCallback, useEffect, useMemo, useState } from 'react';
import {
  addTaskTimelineEntry,
  archiveTaskItem,
  createSavedView,
  createTaskItem,
  createTaskTemplate,
  deleteSavedView,
  deleteTaskTemplate,
  getTaskItem,
  getTaskTemplate,
  listArchiveResolutions,
  listProjects,
  listSavedViews,
  listTaskItems,
  listTaskTemplates,
  reopenTaskItem,
  updateSavedView,
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
  ProjectResponse,
  SavedViewArchiveFilter,
  SavedViewFollowUpFilter,
  SavedViewResponse,
  SavedViewScope,
  SavedViewSortDirection,
  SavedViewSortField,
  TaskItemDetailResponse,
  TaskItemSummaryResponse,
  TaskTemplateDetailResponse,
  UpdateTaskItemRequest,
  UpsertFieldDefinitionRequest,
} from './types';

type WorkspaceMode = 'tasks' | 'templates';
type StatusFilterMode = 'any' | 'empty' | 'exact';

type IconName =
  | 'archive'
  | 'check'
  | 'clock'
  | 'close'
  | 'edit'
  | 'inbox'
  | 'list'
  | 'note'
  | 'panel'
  | 'plus'
  | 'refresh'
  | 'search'
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

interface EditableViewDraft {
  name: string;
  scope: SavedViewScope;
  projectId: string;
  statusMode: StatusFilterMode;
  statusValue: string;
  archive: SavedViewArchiveFilter;
  followUp: '' | SavedViewFollowUpFilter;
  notViewedSinceDays: string;
  notTouchedSinceDays: string;
  text: string;
  sortField: SavedViewSortField;
  sortDirection: SavedViewSortDirection;
  sortOrder: string;
}

const fieldTypes: FieldDefinitionType[] = [
  'Text',
  'LongText',
  'Date',
  'Checkbox',
  'Select',
];

const archiveFilters: SavedViewArchiveFilter[] = ['Active', 'Archived', 'All'];
const followUpFilters: SavedViewFollowUpFilter[] = [
  'Any',
  'Overdue',
  'Today',
  'ThisWeek',
];
const sortFields: SavedViewSortField[] = [
  'lastTouchedAt',
  'createdAt',
  'followUpAt',
  'title',
  'status',
];
const sortDirections: SavedViewSortDirection[] = ['desc', 'asc'];
const staleAfterDays = 14;

function App() {
  const [savedViews, setSavedViews] = useState<SavedViewResponse[]>([]);
  const [projects, setProjects] = useState<ProjectResponse[]>([]);
  const [taskItems, setTaskItems] = useState<TaskItemSummaryResponse[]>([]);
  const [viewCounts, setViewCounts] = useState<Record<string, number>>({});
  const [archiveResolutions, setArchiveResolutions] = useState<ArchiveResolutionResponse[]>([]);
  const [templates, setTemplates] = useState<TaskTemplateDetailResponse[]>([]);
  const [mode, setMode] = useState<WorkspaceMode>(getInitialMode);
  const [currentViewId, setCurrentViewId] = useState<string | null>(getInitialViewId);
  const [selectedTaskId, setSelectedTaskId] = useState<string | null>(null);
  const [selectedTask, setSelectedTask] = useState<TaskItemDetailResponse | null>(null);
  const [editingView, setEditingView] = useState<SavedViewResponse | null>(null);
  const [viewEditorIsOpen, setViewEditorIsOpen] = useState(false);
  const [archiveDialogIsOpen, setArchiveDialogIsOpen] = useState(false);
  const [sidebarIsCollapsed, setSidebarIsCollapsed] = useState(false);
  const [isLoadingWorkspace, setIsLoadingWorkspace] = useState(true);
  const [isLoadingDetail, setIsLoadingDetail] = useState(false);
  const [hasBootstrapped, setHasBootstrapped] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const currentView = useMemo(
    () => savedViews.find((view) => view.id === currentViewId) ?? null,
    [currentViewId, savedViews],
  );

  const loadWorkspace = useCallback(
    async (preferredViewId: string | null = currentViewId) => {
      setIsLoadingWorkspace(true);

      try {
        const [views, projectList, resolutions, templateSummaries] = await Promise.all([
          listSavedViews(),
          listProjects(),
          listArchiveResolutions(),
          listTaskTemplates(),
        ]);
        const selectedViewId = pickSavedViewId(views, preferredViewId);
        const [templateDetails, selectedTasks, countEntries] = await Promise.all([
          Promise.all(templateSummaries.map((template) => getTaskTemplate(template.id))),
          selectedViewId ? listTaskItems({ viewId: selectedViewId }) : Promise.resolve([]),
          Promise.all(
            views.map(async (view) => {
              const items = await listTaskItems({ viewId: view.id });
              return [view.id, items.length] as const;
            }),
          ),
        ]);

        setSavedViews(views);
        setProjects(projectList);
        setArchiveResolutions(resolutions);
        setTemplates(templateDetails);
        setCurrentViewId(selectedViewId);
        setTaskItems(selectedTasks);
        setViewCounts(Object.fromEntries(countEntries));
        setErrorMessage(null);

        if (selectedTaskId && !selectedTasks.some((taskItem) => taskItem.id === selectedTaskId)) {
          setSelectedTaskId(null);
          setSelectedTask(null);
        }
      } catch (error) {
        setErrorMessage(getErrorMessage(error));
      } finally {
        setIsLoadingWorkspace(false);
      }
    },
    [currentViewId, selectedTaskId],
  );

  useEffect(() => {
    if (hasBootstrapped) {
      return;
    }

    setHasBootstrapped(true);
    void loadWorkspace(currentViewId);
  }, [currentViewId, hasBootstrapped, loadWorkspace]);

  useEffect(() => {
    if (mode !== 'tasks' || !selectedTaskId) {
      setSelectedTask(null);
      return;
    }

    let requestIsStale = false;
    setIsLoadingDetail(true);

    getTaskItem(selectedTaskId)
      .then((taskItem) => {
        if (requestIsStale) {
          return;
        }

        setSelectedTask(taskItem);
        setTaskItems((currentItems) =>
          currentItems.map((currentItem) =>
            currentItem.id === taskItem.id
              ? {
                  ...currentItem,
                  lastViewedAt: taskItem.lastViewedAt,
                  lastTouchedAt: taskItem.lastTouchedAt,
                }
              : currentItem,
          ),
        );
        setErrorMessage(null);
      })
      .catch((error) => {
        if (!requestIsStale) {
          setErrorMessage(getErrorMessage(error));
        }
      })
      .finally(() => {
        if (!requestIsStale) {
          setIsLoadingDetail(false);
        }
      });

    return () => {
      requestIsStale = true;
    };
  }, [mode, selectedTaskId]);

  const handleSelectSavedView = (viewId: string) => {
    setMode('tasks');
    setCurrentViewId(viewId);
    setSelectedTaskId(null);
    updateUrl('tasks', viewId);
    void loadWorkspace(viewId);
  };

  const handleOpenTemplates = () => {
    setMode('templates');
    setSelectedTaskId(null);
    updateUrl('templates', null);
  };

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
      const inboxViewId = findViewId(savedViews, 'Inbox') ?? currentViewId;
      setMode('tasks');
      setCurrentViewId(inboxViewId);
      setSelectedTaskId(created.id);
      setSelectedTask(created);
      updateUrl('tasks', inboxViewId);
      await loadWorkspace(inboxViewId);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  const handleUpdateTaskItem = async (requestBody: UpdateTaskItemRequest) => {
    if (!selectedTask) {
      return;
    }

    try {
      const updated = await updateTaskItem(selectedTask.id, requestBody);
      setSelectedTask(updated);
      await loadWorkspace(currentViewId);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  const handleUpdateFieldValues = async (fieldValues: FieldValueMap) => {
    await handleUpdateTaskItem({ fieldValues });
  };

  const handleAddTimelineEntry = async (note: string) => {
    if (!selectedTask) {
      return;
    }

    try {
      const updated = await addTaskTimelineEntry(selectedTask.id, { note });
      setSelectedTask(updated);
      await loadWorkspace(currentViewId);
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
      const archiveViewId = findViewId(savedViews, 'Archive') ?? currentViewId;
      setCurrentViewId(archiveViewId);
      setSelectedTaskId(archived.id);
      setSelectedTask(archived);
      setArchiveDialogIsOpen(false);
      updateUrl('tasks', archiveViewId);
      await loadWorkspace(archiveViewId);
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
      const activeViewId = findViewId(savedViews, 'All active') ?? currentViewId;
      setCurrentViewId(activeViewId);
      setSelectedTaskId(reopened.id);
      setSelectedTask(reopened);
      updateUrl('tasks', activeViewId);
      await loadWorkspace(activeViewId);
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

      await loadWorkspace(currentViewId);
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  const handleDeleteTemplate = async (id: string) => {
    try {
      await deleteTaskTemplate(id);
      await loadWorkspace(currentViewId);
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  const handleSaveView = async (
    id: string | null,
    requestBody: Parameters<typeof createSavedView>[0],
  ) => {
    try {
      const savedView = id
        ? await updateSavedView(id, requestBody)
        : await createSavedView(requestBody);
      setMode('tasks');
      setCurrentViewId(savedView.id);
      setEditingView(null);
      setViewEditorIsOpen(false);
      updateUrl('tasks', savedView.id);
      await loadWorkspace(savedView.id);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  const handleDeleteView = async (id: string) => {
    try {
      await deleteSavedView(id);
      setEditingView(null);
      setViewEditorIsOpen(false);
      await loadWorkspace(null);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  return (
    <main className="app-shell" data-sidebar-collapsed={sidebarIsCollapsed}>
      <Sidebar
        counts={viewCounts}
        currentViewId={currentViewId}
        mode={mode}
        onCreateView={() => {
          setEditingView(null);
          setViewEditorIsOpen(true);
        }}
        onOpenTemplates={handleOpenTemplates}
        onRefresh={() => void loadWorkspace(currentViewId)}
        onSelectView={handleSelectSavedView}
        onToggleSidebar={() => setSidebarIsCollapsed((isCollapsed) => !isCollapsed)}
        savedViews={savedViews}
        sidebarIsCollapsed={sidebarIsCollapsed}
        templateCount={templates.length}
      />

      <section className="workspace" aria-label="Task workspace">
        {errorMessage ? (
          <div className="error-banner" role="alert">
            <strong>Something needs attention.</strong>
            <span>{errorMessage}</span>
          </div>
        ) : null}

        {mode === 'templates' ? (
          <TemplatesPage
            isLoading={isLoadingWorkspace}
            onDeleteTemplate={handleDeleteTemplate}
            onSaveTemplate={handleSaveTemplate}
            templates={templates}
          />
        ) : (
          <TaskBoard
            archiveDialogIsOpen={archiveDialogIsOpen}
            archiveResolutions={archiveResolutions}
            currentView={currentView}
            isLoading={isLoadingWorkspace}
            isLoadingDetail={isLoadingDetail}
            onAddTimelineEntry={handleAddTimelineEntry}
            onArchive={handleArchiveTaskItem}
            onCloseArchiveDialog={() => setArchiveDialogIsOpen(false)}
            onCreateTaskItem={handleCreateTaskItem}
            onEditView={() => {
              setEditingView(currentView);
              setViewEditorIsOpen(true);
            }}
            onOpenArchiveDialog={() => setArchiveDialogIsOpen(true)}
            onReopen={handleReopenTaskItem}
            onSelectTaskItem={(id) => {
              setSelectedTaskId((currentId) => (currentId === id ? null : id));
            }}
            onUpdateFieldValues={handleUpdateFieldValues}
            onUpdateTaskItem={handleUpdateTaskItem}
            projects={projects}
            selectedTask={selectedTask}
            selectedTaskId={selectedTaskId}
            taskItems={taskItems}
            templates={templates}
          />
        )}
      </section>

      {viewEditorIsOpen ? (
        <ViewEditorPanel
          onClose={() => {
            setViewEditorIsOpen(false);
            setEditingView(null);
          }}
          onDeleteView={handleDeleteView}
          onSaveView={handleSaveView}
          projects={projects}
          savedView={editingView}
        />
      ) : null}
    </main>
  );
}

function Sidebar({
  counts,
  currentViewId,
  mode,
  onCreateView,
  onOpenTemplates,
  onRefresh,
  onSelectView,
  onToggleSidebar,
  savedViews,
  sidebarIsCollapsed,
  templateCount,
}: {
  counts: Record<string, number>;
  currentViewId: string | null;
  mode: WorkspaceMode;
  onCreateView: () => void;
  onOpenTemplates: () => void;
  onRefresh: () => void;
  onSelectView: (viewId: string) => void;
  onToggleSidebar: () => void;
  savedViews: SavedViewResponse[];
  sidebarIsCollapsed: boolean;
  templateCount: number;
}) {
  return (
    <aside className="sidebar" aria-label="DumpTether navigation">
      <div className="brand">
        <div className="brand-mark">DT</div>
        <div className="brand-copy">
          <p className="brand-name">DumpTether</p>
          <p className="brand-subtitle">Personal task evidence</p>
        </div>
        <button
          className="icon-button sidebar-toggle"
          onClick={onToggleSidebar}
          title={sidebarIsCollapsed ? 'Expand sidebar' : 'Collapse sidebar'}
          type="button"
        >
          <Icon name="panel" />
        </button>
      </div>

      <nav className="view-nav" aria-label="Saved views">
        {savedViews.map((view) => (
          <button
            aria-current={mode === 'tasks' && currentViewId === view.id ? 'page' : undefined}
            className="nav-item"
            key={view.id}
            onClick={() => onSelectView(view.id)}
            title={view.name}
            type="button"
          >
            <Icon name={getViewIcon(view)} />
            <span className="nav-label">{view.name}</span>
            <span className="nav-count">{counts[view.id] ?? 0}</span>
          </button>
        ))}
      </nav>

      <div className="sidebar-actions">
        <button className="nav-item" onClick={onCreateView} type="button">
          <Icon name="plus" />
          <span className="nav-label">New view</span>
          <span className="nav-count">+</span>
        </button>
        <button
          aria-current={mode === 'templates' ? 'page' : undefined}
          className="nav-item"
          onClick={onOpenTemplates}
          type="button"
        >
          <Icon name="templates" />
          <span className="nav-label">Templates</span>
          <span className="nav-count">{templateCount}</span>
        </button>
        <button className="refresh-button" onClick={onRefresh} type="button">
          <Icon name="refresh" />
          <span className="nav-label">Refresh</span>
        </button>
      </div>
    </aside>
  );
}

function TaskBoard({
  archiveDialogIsOpen,
  archiveResolutions,
  currentView,
  isLoading,
  isLoadingDetail,
  onAddTimelineEntry,
  onArchive,
  onCloseArchiveDialog,
  onCreateTaskItem,
  onEditView,
  onOpenArchiveDialog,
  onReopen,
  onSelectTaskItem,
  onUpdateFieldValues,
  onUpdateTaskItem,
  projects,
  selectedTask,
  selectedTaskId,
  taskItems,
  templates,
}: {
  archiveDialogIsOpen: boolean;
  archiveResolutions: ArchiveResolutionResponse[];
  currentView: SavedViewResponse | null;
  isLoading: boolean;
  isLoadingDetail: boolean;
  onAddTimelineEntry: (note: string) => Promise<void>;
  onArchive: (requestBody: ArchiveTaskItemRequest) => Promise<void>;
  onCloseArchiveDialog: () => void;
  onCreateTaskItem: (
    title: string,
    taskTemplateId: string | null,
    fieldValues: FieldValueMap,
  ) => Promise<void>;
  onEditView: () => void;
  onOpenArchiveDialog: () => void;
  onReopen: (note?: string) => Promise<void>;
  onSelectTaskItem: (id: string) => void;
  onUpdateFieldValues: (fieldValues: FieldValueMap) => Promise<void>;
  onUpdateTaskItem: (requestBody: UpdateTaskItemRequest) => Promise<void>;
  projects: ProjectResponse[];
  selectedTask: TaskItemDetailResponse | null;
  selectedTaskId: string | null;
  taskItems: TaskItemSummaryResponse[];
  templates: TaskTemplateDetailResponse[];
}) {
  const canCreateTask = currentView?.filter.archive !== 'Archived';

  return (
    <section className="task-board" aria-labelledby="task-board-title">
      <div className="board-header">
        <div>
          <p className="detail-kicker">Saved view</p>
          <h1 id="task-board-title">{currentView?.name ?? 'Tasks'}</h1>
          <p>{currentView ? describeSavedView(currentView, projects) : 'Loading views...'}</p>
        </div>
        <div className="board-actions">
          <span className="sort-pill">
            Sorted by {formatSortField(currentView?.sort.field)}{' '}
            {currentView?.sort.direction === 'asc' ? 'ascending' : 'descending'}
          </span>
          <button className="secondary-action" disabled={!currentView} onClick={onEditView} type="button">
            <Icon name="edit" />
            <span>Edit view</span>
          </button>
        </div>
      </div>

      {canCreateTask ? (
        <CreateTaskForm onCreateTaskItem={onCreateTaskItem} templates={templates} />
      ) : null}

      <div className="task-grid" aria-busy={isLoading}>
        {isLoading ? <p className="empty-copy">Loading tasks...</p> : null}
        {!isLoading && taskItems.length === 0 ? (
          <p className="empty-copy board-empty">Nothing matches this view yet.</p>
        ) : null}

        {taskItems.map((taskItem) => {
          const isExpanded = selectedTaskId === taskItem.id;

          return (
            <article
              className="task-card"
              data-expanded={isExpanded}
              data-state={getTaskState(taskItem)}
              key={taskItem.id}
            >
              <button
                className="task-card-button"
                onClick={() => onSelectTaskItem(taskItem.id)}
                type="button"
              >
                <span className="task-card-main">
                  <span className="task-card-title">{taskItem.title}</span>
                  <span className="task-card-latest">
                    {taskItem.latestTimelineEntry?.summary ?? 'No timeline evidence yet'}
                  </span>
                </span>
                <span className="task-card-meta">
                  <span>{taskItem.status || 'No status'}</span>
                  <span>Touched {formatRelativeDate(taskItem.lastTouchedAt)}</span>
                  {taskItem.followUpAt ? (
                    <span>Follow-up {formatShortDate(taskItem.followUpAt)}</span>
                  ) : null}
                </span>
                <TaskBadges taskItem={taskItem} />
              </button>

              {isExpanded ? (
                <div className="task-card-detail">
                  {isLoadingDetail || !selectedTask ? (
                    <p className="empty-copy">Opening task evidence...</p>
                  ) : (
                    <TaskDetail
                      archiveDialogIsOpen={archiveDialogIsOpen}
                      archiveResolutions={archiveResolutions}
                      onAddTimelineEntry={onAddTimelineEntry}
                      onArchive={onArchive}
                      onCloseArchiveDialog={onCloseArchiveDialog}
                      onOpenArchiveDialog={onOpenArchiveDialog}
                      onReopen={onReopen}
                      onUpdateFieldValues={onUpdateFieldValues}
                      onUpdateTaskItem={onUpdateTaskItem}
                      taskItem={selectedTask}
                    />
                  )}
                </div>
              ) : null}
            </article>
          );
        })}
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
  archiveDialogIsOpen,
  archiveResolutions,
  onAddTimelineEntry,
  onArchive,
  onCloseArchiveDialog,
  onOpenArchiveDialog,
  onReopen,
  onUpdateFieldValues,
  onUpdateTaskItem,
  taskItem,
}: {
  archiveDialogIsOpen: boolean;
  archiveResolutions: ArchiveResolutionResponse[];
  onAddTimelineEntry: (note: string) => Promise<void>;
  onArchive: (requestBody: ArchiveTaskItemRequest) => Promise<void>;
  onCloseArchiveDialog: () => void;
  onOpenArchiveDialog: () => void;
  onReopen: (note?: string) => Promise<void>;
  onUpdateFieldValues: (fieldValues: FieldValueMap) => Promise<void>;
  onUpdateTaskItem: (requestBody: UpdateTaskItemRequest) => Promise<void>;
  taskItem: TaskItemDetailResponse;
}) {
  const [reopenNote, setReopenNote] = useState('');
  const [fieldDraft, setFieldDraft] = useState<FieldValueMap>({});
  const [isSavingFields, setIsSavingFields] = useState(false);

  useEffect(() => {
    setReopenNote('');
    setFieldDraft(toFieldValueMap(taskItem.fieldValues));
  }, [taskItem]);

  const fieldValuesCanBeEdited = !taskItem.archivedAt && Boolean(taskItem.template);

  return (
    <section className="task-detail" aria-label="Task detail">
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

      <TaskQuickEditForm onUpdateTaskItem={onUpdateTaskItem} taskItem={taskItem} />

      <div className="detail-meta">
        <MetaItem label="Template" value={taskItem.template?.name ?? 'None'} />
        <MetaItem label="Status" value={taskItem.status ?? 'No status'} />
        <MetaItem label="Created" value={formatDateTime(taskItem.createdAt)} />
        <MetaItem label="Viewed" value={taskItem.lastViewedAt ? formatDateTime(taskItem.lastViewedAt) : 'Never'} />
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
              <Icon name="check" />
              <span>Save fields</span>
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
          <FieldValueList fieldValues={taskItem.fieldValues} template={taskItem.template} />
        )}
      </section>

      <TimelinePanel
        onAddTimelineEntry={onAddTimelineEntry}
        timelineEntries={taskItem.timelineEntries}
      />

      {archiveDialogIsOpen ? (
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

function TaskQuickEditForm({
  onUpdateTaskItem,
  taskItem,
}: {
  onUpdateTaskItem: (requestBody: UpdateTaskItemRequest) => Promise<void>;
  taskItem: TaskItemDetailResponse;
}) {
  const [title, setTitle] = useState(taskItem.title);
  const [status, setStatus] = useState(taskItem.status ?? '');
  const [followUpDate, setFollowUpDate] = useState(toDateInputValue(taskItem.followUpAt));
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    setTitle(taskItem.title);
    setStatus(taskItem.status ?? '');
    setFollowUpDate(toDateInputValue(taskItem.followUpAt));
  }, [taskItem]);

  if (taskItem.archivedAt) {
    return null;
  }

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    setIsSubmitting(true);
    await onUpdateTaskItem({
      title: title.trim(),
      status: status.trim() || null,
      followUpAt: followUpDate
        ? new Date(`${followUpDate}T12:00:00`).toISOString()
        : null,
    });
    setIsSubmitting(false);
  };

  return (
    <form className="quick-edit-form" onSubmit={handleSubmit}>
      <label>
        Title
        <input
          onChange={(event) => setTitle(event.target.value)}
          required
          type="text"
          value={title}
        />
      </label>
      <label>
        Status
        <input
          onChange={(event) => setStatus(event.target.value)}
          placeholder="No status"
          type="text"
          value={status}
        />
      </label>
      <label>
        Follow-up
        <input
          onChange={(event) => setFollowUpDate(event.target.value)}
          type="date"
          value={followUpDate}
        />
      </label>
      <button disabled={!title.trim() || isSubmitting} type="submit">
        Save
      </button>
    </form>
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
        <div className="board-header">
          <div>
            <p className="detail-kicker">Template structure</p>
            <h1 id="templates-title">Templates</h1>
            <p>Define reusable fields for the different shapes a task can take.</p>
          </div>
          <button onClick={() => setSelectedTemplateId(null)} type="button">
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
              onClick={() => setSelectedTemplateId(template.id)}
              type="button"
            >
              <span>{template.name}</span>
              <strong>{template.fields.length} fields</strong>
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
        {fields.length === 0 ? <p className="empty-copy">No fields yet.</p> : null}

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

function ViewEditorPanel({
  onClose,
  onDeleteView,
  onSaveView,
  projects,
  savedView,
}: {
  onClose: () => void;
  onDeleteView: (id: string) => Promise<void>;
  onSaveView: (
    id: string | null,
    requestBody: Parameters<typeof createSavedView>[0],
  ) => Promise<void>;
  projects: ProjectResponse[];
  savedView: SavedViewResponse | null;
}) {
  const [draft, setDraft] = useState<EditableViewDraft>(() =>
    toEditableViewDraft(savedView),
  );
  const [isSubmitting, setIsSubmitting] = useState(false);

  const canSubmit =
    draft.name.trim().length > 0 &&
    (draft.scope === 'Workspace' || draft.projectId.length > 0);

  const updateDraft = (update: Partial<EditableViewDraft>) => {
    setDraft((currentDraft) => ({ ...currentDraft, ...update }));
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    if (!canSubmit) {
      return;
    }

    setIsSubmitting(true);
    await onSaveView(savedView?.id ?? null, toSavedViewRequest(draft));
    setIsSubmitting(false);
  };

  return (
    <div className="dialog-backdrop" role="presentation">
      <section
        aria-labelledby="view-editor-title"
        aria-modal="true"
        className="view-editor-panel"
        role="dialog"
      >
        <div className="dialog-header">
          <div>
            <p className="detail-kicker">{savedView ? 'Edit saved view' : 'New saved view'}</p>
            <h2 id="view-editor-title">{savedView?.name ?? 'Saved view'}</h2>
          </div>
          <button className="icon-button" onClick={onClose} type="button">
            <Icon name="close" />
            <span className="sr-only">Close view editor</span>
          </button>
        </div>

        <form className="view-editor-form" onSubmit={handleSubmit}>
          <label>
            Name
            <input
              onChange={(event) => updateDraft({ name: event.target.value })}
              required
              type="text"
              value={draft.name}
            />
          </label>

          <div className="editor-grid">
            <label>
              Scope
              <select
                onChange={(event) =>
                  updateDraft({ scope: event.target.value as SavedViewScope })
                }
                value={draft.scope}
              >
                <option value="Workspace">Workspace</option>
                <option value="Project">Project</option>
              </select>
            </label>

            <label>
              Project
              <select
                onChange={(event) => updateDraft({ projectId: event.target.value })}
                value={draft.projectId}
              >
                <option value="">All projects</option>
                {projects.map((project) => (
                  <option key={project.id} value={project.id}>
                    {project.name}
                  </option>
                ))}
              </select>
            </label>

            <label>
              Active or archive
              <select
                onChange={(event) =>
                  updateDraft({ archive: event.target.value as SavedViewArchiveFilter })
                }
                value={draft.archive}
              >
                {archiveFilters.map((filter) => (
                  <option key={filter} value={filter}>
                    {filter}
                  </option>
                ))}
              </select>
            </label>

            <label>
              Follow-up
              <select
                onChange={(event) =>
                  updateDraft({
                    followUp: event.target.value as '' | SavedViewFollowUpFilter,
                  })
                }
                value={draft.followUp}
              >
                <option value="">Any follow-up state</option>
                {followUpFilters.map((filter) => (
                  <option key={filter} value={filter}>
                    {formatFollowUpFilter(filter)}
                  </option>
                ))}
              </select>
            </label>
          </div>

          <div className="editor-grid">
            <label>
              Status filter
              <select
                onChange={(event) =>
                  updateDraft({ statusMode: event.target.value as StatusFilterMode })
                }
                value={draft.statusMode}
              >
                <option value="any">Any status</option>
                <option value="empty">No status</option>
                <option value="exact">Exact status</option>
              </select>
            </label>

            <label>
              Status text
              <input
                disabled={draft.statusMode !== 'exact'}
                onChange={(event) => updateDraft({ statusValue: event.target.value })}
                placeholder="Waiting"
                type="text"
                value={draft.statusValue}
              />
            </label>

            <label>
              Not viewed for days
              <input
                min={1}
                onChange={(event) => updateDraft({ notViewedSinceDays: event.target.value })}
                type="number"
                value={draft.notViewedSinceDays}
              />
            </label>

            <label>
              Not touched for days
              <input
                min={1}
                onChange={(event) => updateDraft({ notTouchedSinceDays: event.target.value })}
                type="number"
                value={draft.notTouchedSinceDays}
              />
            </label>
          </div>

          <label>
            Text search
            <input
              onChange={(event) => updateDraft({ text: event.target.value })}
              placeholder="Title, status, or timeline evidence"
              type="search"
              value={draft.text}
            />
          </label>

          <div className="editor-grid">
            <label>
              Sort field
              <select
                onChange={(event) =>
                  updateDraft({ sortField: event.target.value as SavedViewSortField })
                }
                value={draft.sortField}
              >
                {sortFields.map((field) => (
                  <option key={field} value={field}>
                    {formatSortField(field)}
                  </option>
                ))}
              </select>
            </label>

            <label>
              Sort direction
              <select
                onChange={(event) =>
                  updateDraft({
                    sortDirection: event.target.value as SavedViewSortDirection,
                  })
                }
                value={draft.sortDirection}
              >
                {sortDirections.map((direction) => (
                  <option key={direction} value={direction}>
                    {direction === 'asc' ? 'Ascending' : 'Descending'}
                  </option>
                ))}
              </select>
            </label>

            <label>
              Sidebar order
              <input
                onChange={(event) => updateDraft({ sortOrder: event.target.value })}
                type="number"
                value={draft.sortOrder}
              />
            </label>
          </div>

          <div className="dialog-actions">
            {savedView ? (
              <button
                className="ghost-button danger-button"
                onClick={() => void onDeleteView(savedView.id)}
                type="button"
              >
                Delete
              </button>
            ) : null}
            <button className="ghost-button" onClick={onClose} type="button">
              Cancel
            </button>
            <button disabled={!canSubmit || isSubmitting} type="submit">
              Save view
            </button>
          </div>
        </form>
      </section>
    </div>
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
            <Icon name="close" />
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

function TaskBadges({ taskItem }: { taskItem: TaskItemSummaryResponse }) {
  const badges = getTaskBadges(taskItem);

  return (
    <span className="task-badges" aria-label={badges.join(', ')}>
      {badges.map((badge) => (
        <span className="task-badge" key={badge}>
          {badge}
        </span>
      ))}
    </span>
  );
}

function Icon({ name }: { name: IconName }) {
  const paths: Record<IconName, string> = {
    archive: 'M4 7h16v13H4V7Zm2-4h12l2 4H4l2-4Zm5 8h2',
    check: 'm5 13 4 4L19 7',
    clock: 'M12 4a8 8 0 1 0 0 16 8 8 0 0 0 0-16Zm0 4v5l3 2',
    close: 'M6 6l12 12M18 6 6 18',
    edit: 'M4 20h4l10-10-4-4L4 16v4Zm12-16 4 4',
    inbox: 'M4 5h16v10l-3 4H7l-3-4V5Zm0 10h5l1.5 2h3L15 15h5',
    list: 'M8 6h12M8 12h12M8 18h12M4 6h.01M4 12h.01M4 18h.01',
    note: 'M5 4h11l3 3v13H5V4Zm11 0v4h4M8 12h8M8 16h6',
    panel: 'M4 5h16v14H4V5Zm5 0v14',
    plus: 'M12 5v14M5 12h14',
    refresh: 'M20 7v5h-5M4 17v-5h5M18 10a6 6 0 0 0-10-4L4 10m2 4a6 6 0 0 0 10 4l4-4',
    search: 'M11 4a7 7 0 1 0 0 14 7 7 0 0 0 0-14Zm5 12 4 4',
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
      <path
        d={paths[name]}
        stroke="currentColor"
        strokeLinecap="round"
        strokeLinejoin="round"
        strokeWidth="1.8"
      />
    </svg>
  );
}

function pickSavedViewId(
  views: SavedViewResponse[],
  preferredViewId: string | null,
) {
  if (preferredViewId && views.some((view) => view.id === preferredViewId)) {
    return preferredViewId;
  }

  return findViewId(views, 'Inbox') ?? views[0]?.id ?? null;
}

function findViewId(views: SavedViewResponse[], name: string) {
  return views.find((view) => view.name.toLowerCase() === name.toLowerCase())?.id ?? null;
}

function getInitialMode(): WorkspaceMode {
  return new URL(window.location.href).searchParams.get('view') === 'templates'
    ? 'templates'
    : 'tasks';
}

function getInitialViewId() {
  return new URL(window.location.href).searchParams.get('viewId');
}

function updateUrl(mode: WorkspaceMode, viewId: string | null) {
  const url = new URL(window.location.href);

  if (mode === 'templates') {
    url.searchParams.set('view', 'templates');
    url.searchParams.delete('viewId');
  } else {
    url.searchParams.delete('view');
    if (viewId) {
      url.searchParams.set('viewId', viewId);
    } else {
      url.searchParams.delete('viewId');
    }
  }

  window.history.replaceState(null, '', url);
}

function getViewIcon(view: SavedViewResponse): IconName {
  if (view.filter.archive === 'Archived') {
    return 'archive';
  }

  if (view.filter.followUp) {
    return 'clock';
  }

  if (view.filter.status?.toLowerCase().includes('waiting')) {
    return 'waiting';
  }

  if (view.name.toLowerCase().includes('inbox')) {
    return 'inbox';
  }

  return 'list';
}

function describeSavedView(view: SavedViewResponse, projects: ProjectResponse[]) {
  const pieces: string[] = [];
  const project = view.filter.projectId
    ? projects.find((candidate) => candidate.id === view.filter.projectId)
    : null;

  pieces.push(project ? project.name : 'All projects');
  pieces.push(view.filter.archive ?? 'Active');

  if (view.filter.status === '') {
    pieces.push('no status');
  } else if (view.filter.status) {
    pieces.push(`status ${view.filter.status}`);
  }

  if (view.filter.followUp) {
    pieces.push(formatFollowUpFilter(view.filter.followUp));
  }

  if (view.filter.notViewedSinceDays) {
    pieces.push(`not viewed ${view.filter.notViewedSinceDays}d`);
  }

  if (view.filter.notTouchedSinceDays) {
    pieces.push(`not touched ${view.filter.notTouchedSinceDays}d`);
  }

  if (view.filter.text) {
    pieces.push(`search "${view.filter.text}"`);
  }

  return pieces.join(' / ');
}

function getTaskState(taskItem: TaskItemSummaryResponse) {
  if (taskItem.archivedAt) {
    return 'archived';
  }

  if (isFollowUpOverdue(taskItem)) {
    return 'overdue';
  }

  if (isWaiting(taskItem)) {
    return 'waiting';
  }

  if (isStale(taskItem)) {
    return 'stale';
  }

  return 'active';
}

function getTaskBadges(taskItem: TaskItemSummaryResponse) {
  const badges: string[] = [];

  if (taskItem.archivedAt) {
    badges.push('Archived');
  }

  if (isFollowUpOverdue(taskItem)) {
    badges.push('Overdue');
  }

  if (isWaiting(taskItem)) {
    badges.push('Waiting');
  }

  if (isStale(taskItem)) {
    badges.push('Stale');
  }

  if (taskItem.followUpAt && !isFollowUpOverdue(taskItem)) {
    badges.push('Follow-up');
  }

  return badges.length > 0 ? badges : ['Active'];
}

function isWaiting(taskItem: TaskItemSummaryResponse) {
  return taskItem.status?.toLowerCase().includes('waiting') ?? false;
}

function isFollowUpOverdue(taskItem: TaskItemSummaryResponse) {
  return Boolean(taskItem.followUpAt) &&
    !taskItem.archivedAt &&
    new Date(taskItem.followUpAt!).getTime() < Date.now();
}

function isStale(taskItem: TaskItemSummaryResponse) {
  const touchedAt = new Date(taskItem.lastTouchedAt).getTime();
  const staleAt = Date.now() - staleAfterDays * 24 * 60 * 60 * 1000;
  return !taskItem.archivedAt && touchedAt < staleAt;
}

function toEditableViewDraft(savedView: SavedViewResponse | null): EditableViewDraft {
  const status = savedView?.filter.status;

  return {
    name: savedView?.name ?? '',
    scope: savedView?.scope ?? 'Workspace',
    projectId: savedView?.filter.projectId ?? '',
    statusMode: status === '' ? 'empty' : status ? 'exact' : 'any',
    statusValue: status && status.length > 0 ? status : '',
    archive: savedView?.filter.archive ?? 'Active',
    followUp: savedView?.filter.followUp ?? '',
    notViewedSinceDays: savedView?.filter.notViewedSinceDays?.toString() ?? '',
    notTouchedSinceDays: savedView?.filter.notTouchedSinceDays?.toString() ?? '',
    text: savedView?.filter.text ?? '',
    sortField: savedView?.sort.field ?? 'lastTouchedAt',
    sortDirection: savedView?.sort.direction ?? 'desc',
    sortOrder: savedView?.sortOrder.toString() ?? '20',
  };
}

function toSavedViewRequest(draft: EditableViewDraft) {
  const status =
    draft.statusMode === 'any'
      ? null
      : draft.statusMode === 'empty'
        ? ''
        : draft.statusValue.trim();

  return {
    name: draft.name.trim(),
    scope: draft.scope,
    filter: {
      projectId: draft.projectId || null,
      status,
      archive: draft.archive,
      followUp: draft.followUp || null,
      notViewedSinceDays: numberOrNull(draft.notViewedSinceDays),
      notTouchedSinceDays: numberOrNull(draft.notTouchedSinceDays),
      text: draft.text.trim() || null,
    },
    sort: {
      field: draft.sortField,
      direction: draft.sortDirection,
    },
    sortOrder: numberOrNull(draft.sortOrder) ?? 20,
  };
}

function numberOrNull(value: string) {
  if (!value.trim()) {
    return null;
  }

  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
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

function formatFollowUpFilter(value: SavedViewFollowUpFilter) {
  return value
    .replace('ThisWeek', 'This week')
    .replace('Any', 'Has follow-up');
}

function formatSortField(value?: SavedViewSortField | null) {
  switch (value) {
    case 'createdAt':
      return 'Created';
    case 'followUpAt':
      return 'Follow-up';
    case 'title':
      return 'Title';
    case 'status':
      return 'Status';
    case 'lastTouchedAt':
    default:
      return 'Last touched';
  }
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

function toDateInputValue(value: string | null) {
  if (!value) {
    return '';
  }

  return new Date(value).toISOString().slice(0, 10);
}

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : 'Unexpected error.';
}

export default App;
