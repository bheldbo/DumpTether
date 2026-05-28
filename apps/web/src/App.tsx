import {
  type CSSProperties,
  FormEvent,
  type KeyboardEvent,
  type MouseEvent,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import {
  addTaskTimelineEntry,
  archiveTaskItem,
  createProject,
  createSavedView,
  createTaskItem,
  createTaskTemplate,
  createWorkspace,
  deleteSavedView,
  deleteTaskTimelineEntry,
  deleteTaskTemplate,
  getTaskItem,
  getTaskTemplate,
  getWorkspace,
  listArchiveResolutions,
  listProjects,
  listSavedViews,
  listTaskItems,
  listTaskTemplates,
  listWorkspaces,
  reopenTaskItem,
  setCurrentWorkspaceId,
  updateSavedView,
  updateProject,
  updateTaskItem,
  updateTaskTimelineEntry,
  updateTaskTemplate,
  updateWorkspace,
} from './api';
import './App.css';
import { FieldEditorList, FieldValueList } from './fieldRenderers';
import { toFieldValueMap } from './fieldValues';
import type {
  ArchiveResolutionResponse,
  ArchiveTaskItemRequest,
  CreateTaskItemRequest,
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
  UpdateProjectRequest,
  UpdateTaskItemRequest,
  UpdateWorkspaceRequest,
  UpsertFieldDefinitionRequest,
  WorkspaceResponse,
} from './types';

type WorkspaceMode = 'tasks' | 'templates';
type StatusFilterMode = 'any' | 'empty' | 'exact';
type Language = 'en' | 'da';
type Translate = (key: TranslationKey) => string;

type IconName =
  | 'archive'
  | 'arrowDown'
  | 'arrowUp'
  | 'back'
  | 'check'
  | 'clock'
  | 'close'
  | 'edit'
  | 'filterOff'
  | 'inbox'
  | 'list'
  | 'note'
  | 'palette'
  | 'panel'
  | 'plus'
  | 'refresh'
  | 'search'
  | 'settings'
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
  category: string;
  color: string;
  archive: SavedViewArchiveFilter;
  followUp: '' | SavedViewFollowUpFilter;
  notViewedSinceDays: string;
  notTouchedSinceDays: string;
  text: string;
  sortField: SavedViewSortField;
  sortDirection: SavedViewSortDirection;
  sortOrder: string;
}

interface TaskWallFilters {
  text: string;
  status: string;
  category: string;
  color: string;
  projectId: string;
  notTouchedDays: string;
  followUp: '' | SavedViewFollowUpFilter;
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
const colorChoices = [
  '#FDE68A',
  '#FCA5A5',
  '#93C5FD',
  '#86EFAC',
  '#C4B5FD',
  '#FDBA74',
  '#CBD5E1',
];
const languageStorageKey = 'dumptether.language';
const workspaceStorageKey = 'dumptether.workspace';
const translations = {
  en: {
    addTask: 'Add task',
    allProjects: 'All categories',
    anyCategory: 'Any category',
    anyColor: 'Any color',
    anyFollowUp: 'Any follow-up',
    anyProject: 'Any category',
    anyStatus: 'Any status',
    archive: 'Archive',
    archiveAction: 'Archive',
    archiveTasks: 'Archive task(s)',
    collapseSidebar: 'Collapse sidebar',
    color: 'Color',
    danish: 'Danish',
    activeTask: 'Active task',
    archivedTask: 'Archived task',
    backToWall: 'Back to wall',
    board: 'Board',
    boardColor: 'Board color',
    cancel: 'Cancel',
    category: 'Category',
    confirmDelete: 'Sure?',
    created: 'Created',
    editView: 'Edit filter',
    editBoard: 'Edit board',
    editProject: 'Edit project',
    editTask: 'Edit task',
    english: 'English',
    expandSidebar: 'Expand sidebar',
    fieldsForFiltering: 'Fields',
    fieldsHelp: 'Structured fields make filtering possible without turning the wall into a form.',
    filterWall: 'Filter wall...',
    followUpDate: 'Follow-up date',
    keep: 'Keep',
    language: 'Language',
    lastUpdated: 'Last updated',
    loadingTasks: 'Loading tasks...',
    newTask: 'New task',
    newTaskPlaceholder: 'Add a task and press Enter...',
    newView: 'Save filter',
    note: 'Note',
    noteCount: 'notes',
    notes: 'Notes',
    noTaskColors: 'No task colors yet',
    noTasks: 'Nothing here yet. Use + to add your first task.',
    noTasksMatch: 'No tasks match these filters. Reset filters to see the whole wall again.',
    noCategory: 'No category',
    noFollowUp: 'No follow-up',
    noStatus: 'No status',
    notTouchedDays: 'Not touched days',
    overview: 'All tasks',
    refresh: 'Refresh',
    resetFilters: 'Reset filters',
    saved: 'Saved',
    saveFailed: 'Save failed',
    savedViews: 'Wall',
    saveFields: 'Save fields',
    save: 'Save',
    saving: 'Saving...',
    settings: 'Settings',
    sortAscending: 'ascending',
    sortDescending: 'descending',
    sortedBy: 'Sorted by',
    status: 'Status',
    templates: 'Task templates',
    sortCreated: 'Created',
    sortFollowUp: 'Follow-up',
    sortLastTouched: 'Last updated',
    sortStatus: 'Status',
    sortTitle: 'Title',
    projectTags: 'Categories',
    noNotesYet: 'No notes yet',
    followUp: 'Follow-up',
    workspaceColor: 'Workspace color',
    taskColor: 'Task color',
    deleteNote: 'Delete note',
    done: 'Stop selecting',
    editMode: 'Edit mode',
    archiveSelected: 'Archive selected',
    selectedTasks: 'selected',
    undo: 'Undo',
    addNotePlaceholder: 'Add a note and press Enter...',
    clearColor: 'Clear color',
    noColor: 'No color',
    wallHelp: 'Search tasks by text, category, status, field values, color, and dates.',
    workspaces: 'Boards',
    allWorkspaces: 'All boards',
    newWorkspace: 'New board',
    newProjectTag: 'New category',
    removeFilters: 'Remove filters',
    cleanup: 'Cleanup',
    clearArchive: 'Clear archive',
    clearOldTasks: 'Clear old tasks...',
    clearWorkspaceTasks: 'Clear board tasks...',
    deleteBoard: 'Delete board...',
    deleteProjectTag: 'Delete project tag...',
    cleanupFuture: 'Cleanup actions will land with board safety checks.',
  },
  da: {
    addTask: 'Tilføj opgave',
    allProjects: 'Alle kategorier',
    anyCategory: 'Alle kategorier',
    anyColor: 'Alle farver',
    anyFollowUp: 'Alle opfølgninger',
    anyProject: 'Alle kategorier',
    anyStatus: 'Alle statusser',
    archive: 'Arkiv',
    archiveAction: 'Arkivér',
    archiveTasks: 'Arkivér opgave(r)',
    collapseSidebar: 'Skjul sidebar',
    color: 'Farve',
    danish: 'Dansk',
    activeTask: 'Aktiv opgave',
    archivedTask: 'Arkiveret opgave',
    backToWall: 'Tilbage til tavlen',
    board: 'Tavle',
    boardColor: 'Tavlefarve',
    cancel: 'Annuller',
    category: 'Kategori',
    confirmDelete: 'Sikker?',
    created: 'Oprettet',
    editView: 'Rediger filter',
    editBoard: 'Rediger tavle',
    editProject: 'Rediger projekt',
    editTask: 'Rediger opgave',
    english: 'Engelsk',
    expandSidebar: 'Vis sidebar',
    fieldsForFiltering: 'Felter',
    fieldsHelp: 'Strukturerede felter gør det muligt at filtrere uden at gøre tavlen til en stor formular.',
    filterWall: 'Filtrer tavlen...',
    followUpDate: 'Opfølgning',
    keep: 'Behold',
    language: 'Sprog',
    lastUpdated: 'Sidst opdateret',
    loadingTasks: 'Indlæser opgaver...',
    newTask: 'Ny opgave',
    newTaskPlaceholder: 'Tilføj en opgave og tryk Enter...',
    newView: 'Gem filter',
    note: 'Note',
    noteCount: 'noter',
    notes: 'Noter',
    noTaskColors: 'Ingen opgavefarver endnu',
    noTasks: 'Her er tomt endnu. Brug + for at tilføje din første opgave.',
    noTasksMatch: 'Ingen opgaver matcher filtrene. Nulstil filtrene for at se hele tavlen igen.',
    noCategory: 'Ingen kategori',
    noFollowUp: 'Ingen opfølgning',
    noStatus: 'Ingen status',
    notTouchedDays: 'Ikke rørt i dage',
    overview: 'Alle opgaver',
    refresh: 'Opdater',
    resetFilters: 'Nulstil filtre',
    saved: 'Gemt',
    saveFailed: 'Kunne ikke gemme',
    savedViews: 'Tavle',
    saveFields: 'Gem felter',
    save: 'Gem',
    saving: 'Gemmer...',
    settings: 'Indstillinger',
    sortAscending: 'stigende',
    sortDescending: 'faldende',
    sortedBy: 'Sorteret efter',
    status: 'Status',
    templates: 'Opgaveskabeloner',
    sortCreated: 'Oprettet',
    sortFollowUp: 'Opfølgning',
    sortLastTouched: 'Sidst opdateret',
    sortStatus: 'Status',
    sortTitle: 'Titel',
    projectTags: 'Kategorier',
    noNotesYet: 'Ingen noter endnu',
    followUp: 'Opfølgning',
    workspaceColor: 'Tavlefarve',
    taskColor: 'Opgavefarve',
    deleteNote: 'Slet note',
    done: 'Stop valg',
    editMode: 'Rediger',
    archiveSelected: 'Arkivér valgte',
    selectedTasks: 'valgt',
    undo: 'Fortryd',
    addNotePlaceholder: 'Tilføj en note og tryk Enter...',
    clearColor: 'Ryd farve',
    noColor: 'Ingen farve',
    wallHelp: 'Søg i opgaver efter tekst, kategori, status, feltværdier, farve og datoer.',
    workspaces: 'Tavler',
    allWorkspaces: 'Alle tavler',
    newWorkspace: 'Ny tavle',
    newProjectTag: 'Ny kategori',
    removeFilters: 'Fjern filtre',
    cleanup: 'Oprydning',
    clearArchive: 'Ryd arkiv',
    clearOldTasks: 'Ryd gamle opgaver...',
    clearWorkspaceTasks: 'Ryd opgaver i tavle...',
    deleteBoard: 'Slet tavle...',
    deleteProjectTag: 'Slet kategori...',
    cleanupFuture: 'Oprydning kommer med sikkerhedstjek for tavler.',
  },
} as const;
type TranslationKey = keyof typeof translations.en;

function App() {
  const [savedViews, setSavedViews] = useState<SavedViewResponse[]>([]);
  const [workspaces, setWorkspaces] = useState<WorkspaceResponse[]>([]);
  const [workspace, setWorkspace] = useState<WorkspaceResponse | null>(null);
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
  const [settingsIsOpen, setSettingsIsOpen] = useState(false);
  const [language, setLanguage] = useState<Language>(getInitialLanguage);
  const [selectedWorkspaceId, setSelectedWorkspaceId] = useState<string | null>(
    getInitialWorkspaceId,
  );
  const [taskColorOptions, setTaskColorOptions] = useState<string[]>([]);
  const [isLoadingWorkspace, setIsLoadingWorkspace] = useState(true);
  const [isLoadingDetail, setIsLoadingDetail] = useState(false);
  const [hasBootstrapped, setHasBootstrapped] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const t = useCallback<Translate>((key) => translate(language, key), [language]);

  const currentView = useMemo(
    () => savedViews.find((view) => view.id === currentViewId) ?? null,
    [currentViewId, savedViews],
  );

  const loadWorkspace = useCallback(
    async (
      preferredViewId: string | null = currentViewId,
      preferredWorkspaceId: string | null = selectedWorkspaceId,
    ) => {
      setIsLoadingWorkspace(true);

      try {
        setCurrentWorkspaceId(null);
        const workspaceList = await listWorkspaces();
        const effectiveWorkspaceId =
          preferredWorkspaceId && workspaceList.some((candidate) => candidate.id === preferredWorkspaceId)
            ? preferredWorkspaceId
            : workspaceList[0]?.id ?? null;

        setCurrentWorkspaceId(effectiveWorkspaceId);
        window.localStorage.setItem(workspaceStorageKey, effectiveWorkspaceId ?? '');

        const [workspaceInfo, views, projectList, resolutions, templateSummaries] = await Promise.all([
          getWorkspace(),
          listSavedViews(),
          listProjects(),
          listArchiveResolutions(),
          listTaskTemplates(),
        ]);
        const selectedViewId = pickSavedViewId(views, preferredViewId);
        const [templateDetails, selectedTasks, countEntries, allTasksForColors] = await Promise.all([
          Promise.all(templateSummaries.map((template) => getTaskTemplate(template.id))),
          selectedViewId ? listTaskItems({ viewId: selectedViewId }) : Promise.resolve([]),
          Promise.all(
            views.map(async (view) => {
              const items = await listTaskItems({ viewId: view.id });
              return [view.id, items.length] as const;
            }),
          ),
          listTaskItems({ archive: 'All' }),
        ]);

        setWorkspaces(workspaceList);
        setWorkspace(workspaceInfo);
        setSelectedWorkspaceId(workspaceInfo.id);
        setSavedViews(views);
        setProjects(projectList);
        setArchiveResolutions(resolutions);
        setTemplates(templateDetails);
        setTaskColorOptions(mergeColorOptions(getTaskColors(allTasksForColors)));
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
    [currentViewId, selectedTaskId, selectedWorkspaceId],
  );

  useEffect(() => {
    if (hasBootstrapped) {
      return;
    }

    setHasBootstrapped(true);
    void loadWorkspace(currentViewId);
  }, [currentViewId, hasBootstrapped, loadWorkspace]);

  useEffect(() => {
    window.localStorage.setItem(languageStorageKey, language);
  }, [language]);

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
  }, [mode, selectedTaskId, selectedWorkspaceId]);

  const handleSelectSavedView = (viewId: string) => {
    setMode('tasks');
    setCurrentViewId(viewId);
    setSelectedTaskId(null);
    updateUrl('tasks', viewId);
    void loadWorkspace(viewId);
  };

  const handleSelectWorkspace = (workspaceId: string) => {
    setMode('tasks');
    setSelectedWorkspaceId(workspaceId);
    setCurrentViewId(null);
    setSelectedTaskId(null);
    setSelectedTask(null);
    updateUrl('tasks', null);
    void loadWorkspace(null, workspaceId);
  };

  const handleCreateWorkspace = async (name: string) => {
    try {
      setCurrentWorkspaceId(null);
      const created = await createWorkspace({ name: name.trim() });
      setWorkspaces((currentWorkspaces) => [...currentWorkspaces, created]);
      handleSelectWorkspace(created.id);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  const handleCreateProject = async () => {
    const name = window.prompt(t('newProjectTag'));

    if (!name?.trim()) {
      return;
    }

    try {
      const created = await createProject({ name: name.trim() });
      setProjects((currentProjects) => [...currentProjects, created]);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  const handleOpenTemplates = () => {
    setMode('templates');
    setSelectedTaskId(null);
    updateUrl('templates', null);
  };

  const handleCreateTaskItem = async (
    title: string,
    options: Partial<CreateTaskItemRequest> = {},
  ) => {
    try {
      const created = await createTaskItem({
        title,
        projectId: options.projectId ?? null,
        category: options.category ?? null,
        taskTemplateId: options.taskTemplateId ?? null,
      });
      setMode('tasks');
      setSelectedTaskId(null);
      setSelectedTask(null);
      setTaskItems((currentItems) => [created, ...currentItems]);
      if (currentViewId) {
        setViewCounts((counts) => ({
          ...counts,
          [currentViewId]: (counts[currentViewId] ?? 0) + 1,
        }));
      }
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
      setTaskItems((currentItems) =>
        currentItems.map((taskItem) =>
          taskItem.id === updated.id ? updated : taskItem,
        ),
      );
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  const handleUpdateTimelineEntry = async (entryId: string, note: string) => {
    if (!selectedTask) {
      return;
    }

    try {
      const updated = await updateTaskTimelineEntry(selectedTask.id, entryId, { note });
      setSelectedTask(updated);
      setTaskItems((currentItems) =>
        currentItems.map((taskItem) =>
          taskItem.id === updated.id ? updated : taskItem,
        ),
      );
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  const handleDeleteTimelineEntry = async (entryId: string) => {
    if (!selectedTask) {
      return;
    }

    try {
      const updated = await deleteTaskTimelineEntry(selectedTask.id, entryId);
      setSelectedTask(updated);
      setTaskItems((currentItems) =>
        currentItems.map((taskItem) =>
          taskItem.id === updated.id ? updated : taskItem,
        ),
      );
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

  const handleArchiveTaskItems = async (
    taskItemIds: string[],
    requestBody: ArchiveTaskItemRequest,
  ) => {
    if (taskItemIds.length === 0) {
      return;
    }

    try {
      await Promise.all(
        taskItemIds.map((taskItemId) => archiveTaskItem(taskItemId, requestBody)),
      );
      setSelectedTaskId(null);
      setSelectedTask(null);
      setArchiveDialogIsOpen(false);
      await loadWorkspace(currentViewId);
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
      const activeViewId = findViewId(savedViews, 'All Tasks') ??
        findViewId(savedViews, 'Overview') ??
        currentViewId;
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

  const handleUpdateWorkspace = async (requestBody: UpdateWorkspaceRequest) => {
    try {
      const updated = await updateWorkspace(requestBody);
      setWorkspace(updated);
      setWorkspaces((currentWorkspaces) =>
        currentWorkspaces.map((currentWorkspace) =>
          currentWorkspace.id === updated.id ? updated : currentWorkspace,
        ),
      );
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  const handleUpdateProject = async (id: string, requestBody: UpdateProjectRequest) => {
    try {
      const updated = await updateProject(id, requestBody);
      setProjects((currentProjects) =>
        currentProjects.map((project) =>
          project.id === updated.id ? updated : project,
        ),
      );
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  return (
    <main className="app-shell" data-sidebar-collapsed={sidebarIsCollapsed}>
      <Sidebar
        counts={viewCounts}
        currentViewId={currentViewId}
        language={language}
        mode={mode}
        onCreateWorkspace={handleCreateWorkspace}
        onOpenSettings={() => setSettingsIsOpen(true)}
        onOpenTemplates={handleOpenTemplates}
        onRefresh={() => void loadWorkspace(currentViewId)}
        onSelectWorkspace={handleSelectWorkspace}
        onSelectView={handleSelectSavedView}
        onToggleSidebar={() => setSidebarIsCollapsed((isCollapsed) => !isCollapsed)}
        savedViews={savedViews}
        sidebarIsCollapsed={sidebarIsCollapsed}
        templateCount={templates.length}
        t={t}
        workspace={workspace}
        workspaces={workspaces}
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
            t={t}
          />
        ) : (
          <TaskBoard
            archiveDialogIsOpen={archiveDialogIsOpen}
            archiveResolutions={archiveResolutions}
            currentView={currentView}
            colorOptions={taskColorOptions}
            isLoading={isLoadingWorkspace}
            isLoadingDetail={isLoadingDetail}
            onAddTimelineEntry={handleAddTimelineEntry}
            onArchive={handleArchiveTaskItem}
            onArchiveTaskItems={handleArchiveTaskItems}
            onCloseArchiveDialog={() => setArchiveDialogIsOpen(false)}
            onCreateTaskItem={handleCreateTaskItem}
            onCreateProject={handleCreateProject}
            onDeleteTimelineEntry={handleDeleteTimelineEntry}
            onOpenArchiveDialog={() => setArchiveDialogIsOpen(true)}
            onReopen={handleReopenTaskItem}
            onSelectTaskItem={(id) => {
              setSelectedTaskId(id);
            }}
            onCloseTaskItem={() => {
              setSelectedTaskId(null);
              setSelectedTask(null);
            }}
            onUpdateFieldValues={handleUpdateFieldValues}
            onUpdateTaskItem={handleUpdateTaskItem}
            onUpdateTimelineEntry={handleUpdateTimelineEntry}
            onUpdateProject={handleUpdateProject}
            onUpdateWorkspace={handleUpdateWorkspace}
            projects={projects}
            selectedTask={selectedTask}
            selectedTaskId={selectedTaskId}
            taskItems={taskItems}
            templates={templates}
            t={t}
            workspace={workspace}
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
          colorOptions={taskColorOptions}
          projects={projects}
          savedView={editingView}
          t={t}
        />
      ) : null}

      {settingsIsOpen ? (
        <SettingsPanel
          language={language}
          onChangeLanguage={setLanguage}
          onClose={() => setSettingsIsOpen(false)}
          t={t}
        />
      ) : null}
    </main>
  );
}

function Sidebar({
  counts,
  currentViewId,
  language,
  mode,
  onCreateWorkspace,
  onOpenSettings,
  onOpenTemplates,
  onRefresh,
  onSelectWorkspace,
  onSelectView,
  onToggleSidebar,
  savedViews,
  sidebarIsCollapsed,
  t,
  templateCount,
  workspace,
  workspaces,
}: {
  counts: Record<string, number>;
  currentViewId: string | null;
  language: Language;
  mode: WorkspaceMode;
  onCreateWorkspace: (name: string) => Promise<void>;
  onOpenSettings: () => void;
  onOpenTemplates: () => void;
  onRefresh: () => void;
  onSelectWorkspace: (workspaceId: string) => void;
  onSelectView: (viewId: string) => void;
  onToggleSidebar: () => void;
  savedViews: SavedViewResponse[];
  sidebarIsCollapsed: boolean;
  t: Translate;
  templateCount: number;
  workspace: WorkspaceResponse | null;
  workspaces: WorkspaceResponse[];
}) {
  const [workspaceDraft, setWorkspaceDraft] = useState('');
  const [workspaceCreateIsOpen, setWorkspaceCreateIsOpen] = useState(false);
  const [workspaceIsSubmitting, setWorkspaceIsSubmitting] = useState(false);
  const workspaceInputRef = useRef<HTMLInputElement>(null);
  const visibleSavedViews = useMemo(
    () => savedViews.filter((view) => ['all tasks', 'overview', 'archive'].includes(view.name.toLowerCase())),
    [savedViews],
  );

  useEffect(() => {
    if (workspaceCreateIsOpen) {
      workspaceInputRef.current?.focus();
    }
  }, [workspaceCreateIsOpen]);

  const submitWorkspace = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const trimmedName = workspaceDraft.trim();

    if (!trimmedName) {
      return;
    }

    setWorkspaceIsSubmitting(true);
    await onCreateWorkspace(trimmedName);
    setWorkspaceDraft('');
    setWorkspaceCreateIsOpen(false);
    setWorkspaceIsSubmitting(false);
  };

  return (
    <aside
      className="sidebar"
      aria-label="DumpTether navigation"
      style={getSidebarStyle(workspace?.color ?? null)}
    >
      <div className="brand">
        <div className="brand-mark">DT</div>
        <div className="brand-copy">
          <p className="brand-name">DumpTether</p>
          <p className="brand-subtitle">Personal task evidence</p>
        </div>
        <button
          className="icon-button sidebar-toggle"
          onClick={onToggleSidebar}
          title={sidebarIsCollapsed ? t('expandSidebar') : t('collapseSidebar')}
          type="button"
        >
          <Icon name="panel" />
        </button>
      </div>

      <div className="sidebar-section-label">
        <span>{t('workspaces')}</span>
        <button
          className="tiny-icon-button"
          onClick={() => setWorkspaceCreateIsOpen((isOpen) => !isOpen)}
          title={t('newWorkspace')}
          type="button"
        >
          <Icon name="plus" />
        </button>
      </div>

      <nav className="view-nav workspace-nav" aria-label={t('workspaces')}>
        {workspaces.map((candidate) => (
          <button
            aria-current={workspace?.id === candidate.id ? 'page' : undefined}
            className="nav-item workspace-nav-item"
            key={candidate.id}
            onClick={() => onSelectWorkspace(candidate.id)}
            title={candidate.name}
            type="button"
          >
            <span
              className="workspace-color-dot"
              style={{ backgroundColor: candidate.color ?? '#184c48' }}
            />
            <span className="nav-label">{candidate.name}</span>
          </button>
        ))}
        {workspaceCreateIsOpen ? (
          <form className="sidebar-inline-form" onSubmit={submitWorkspace}>
            <input
              aria-label={t('newWorkspace')}
              onChange={(event) => setWorkspaceDraft(event.target.value)}
              placeholder={t('newWorkspace')}
              ref={workspaceInputRef}
              type="text"
              value={workspaceDraft}
            />
            <button
              className="icon-button"
              disabled={!workspaceDraft.trim() || workspaceIsSubmitting}
              title={t('newWorkspace')}
              type="submit"
            >
              <Icon name="check" />
            </button>
          </form>
        ) : null}
      </nav>

      <div className="sidebar-section-label">
        <span>{t('savedViews')}</span>
      </div>

      <nav className="view-nav" aria-label={t('savedViews')}>
        {visibleSavedViews.map((view) => (
          <button
            aria-current={mode === 'tasks' && currentViewId === view.id ? 'page' : undefined}
            className="nav-item"
            key={view.id}
            onClick={() => onSelectView(view.id)}
            title={formatSavedViewName(view.name, t)}
            type="button"
          >
            <Icon name={getViewIcon(view)} />
            <span className="nav-label">{formatSavedViewName(view.name, t)}</span>
            <span className="nav-count">{counts[view.id] ?? 0}</span>
          </button>
        ))}
      </nav>

      <div className="sidebar-actions">
        <button
          aria-current={mode === 'templates' ? 'page' : undefined}
          className="nav-item"
          onClick={onOpenTemplates}
          type="button"
        >
          <Icon name="templates" />
          <span className="nav-label">{t('templates')}</span>
          <span className="nav-count">{templateCount}</span>
        </button>
        <button className="nav-item" onClick={onOpenSettings} type="button">
          <Icon name="settings" />
          <span className="nav-label">{t('settings')}</span>
          <span className="nav-count">{language.toUpperCase()}</span>
        </button>
        <button className="refresh-button" onClick={onRefresh} type="button">
          <Icon name="refresh" />
          <span className="nav-label">{t('refresh')}</span>
        </button>
      </div>
    </aside>
  );
}

function TaskBoard({
  archiveDialogIsOpen,
  archiveResolutions,
  colorOptions,
  currentView,
  isLoading,
  isLoadingDetail,
  onAddTimelineEntry,
  onArchive,
  onArchiveTaskItems,
  onCloseArchiveDialog,
  onCreateProject,
  onCreateTaskItem,
  onDeleteTimelineEntry,
  onOpenArchiveDialog,
  onReopen,
  onCloseTaskItem,
  onSelectTaskItem,
  onUpdateFieldValues,
  onUpdateProject,
  onUpdateTaskItem,
  onUpdateTimelineEntry,
  onUpdateWorkspace,
  projects,
  selectedTask,
  selectedTaskId,
  taskItems,
  templates,
  t,
  workspace,
}: {
  archiveDialogIsOpen: boolean;
  archiveResolutions: ArchiveResolutionResponse[];
  colorOptions: string[];
  currentView: SavedViewResponse | null;
  isLoading: boolean;
  isLoadingDetail: boolean;
  onAddTimelineEntry: (note: string) => Promise<void>;
  onArchive: (requestBody: ArchiveTaskItemRequest) => Promise<void>;
  onArchiveTaskItems: (taskItemIds: string[], requestBody: ArchiveTaskItemRequest) => Promise<void>;
  onCloseArchiveDialog: () => void;
  onCreateProject: () => void;
  onCreateTaskItem: (
    title: string,
    options?: Partial<CreateTaskItemRequest>,
  ) => Promise<void>;
  onDeleteTimelineEntry: (entryId: string) => Promise<void>;
  onOpenArchiveDialog: () => void;
  onReopen: (note?: string) => Promise<void>;
  onCloseTaskItem: () => void;
  onSelectTaskItem: (id: string) => void;
  onUpdateFieldValues: (fieldValues: FieldValueMap) => Promise<void>;
  onUpdateProject: (id: string, requestBody: UpdateProjectRequest) => Promise<void>;
  onUpdateTaskItem: (requestBody: UpdateTaskItemRequest) => Promise<void>;
  onUpdateTimelineEntry: (entryId: string, note: string) => Promise<void>;
  onUpdateWorkspace: (requestBody: UpdateWorkspaceRequest) => Promise<void>;
  projects: ProjectResponse[];
  selectedTask: TaskItemDetailResponse | null;
  selectedTaskId: string | null;
  taskItems: TaskItemSummaryResponse[];
  templates: TaskTemplateDetailResponse[];
  t: Translate;
  workspace: WorkspaceResponse | null;
}) {
  const canCreateTask = currentView?.filter.archive !== 'Archived';
  const [filters, setFilters] = useState<TaskWallFilters>(emptyTaskWallFilters);
  const [pendingDeletedNoteIds, setPendingDeletedNoteIds] = useState<string[]>([]);
  const [editModeIsEnabled, setEditModeIsEnabled] = useState(false);
  const [selectedTaskIds, setSelectedTaskIds] = useState<string[]>([]);
  const [batchArchiveIsOpen, setBatchArchiveIsOpen] = useState(false);
  const visibleTaskItems = useMemo(
    () => applyTaskWallFilters(taskItems, filters),
    [filters, taskItems],
  );
  const focusedTaskItem = selectedTaskId
    ? visibleTaskItems.find((taskItem) => taskItem.id === selectedTaskId) ?? null
    : null;
  const displayedTaskItems = focusedTaskItem ? [focusedTaskItem] : visibleTaskItems;
  const filterOptions = useMemo(
    () => buildTaskFilterOptions(taskItems, colorOptions),
    [colorOptions, taskItems],
  );
  const filtersAreActive = taskWallFiltersAreActive(filters);
  useEffect(() => {
    setPendingDeletedNoteIds([]);
  }, [selectedTaskId]);

  useEffect(() => {
    setSelectedTaskIds((currentIds) =>
      currentIds.filter((id) => visibleTaskItems.some((taskItem) => taskItem.id === id)),
    );
  }, [visibleTaskItems]);

  const toggleSelectedTask = (taskItemId: string) => {
    setSelectedTaskIds((currentIds) =>
      currentIds.includes(taskItemId)
        ? currentIds.filter((currentId) => currentId !== taskItemId)
        : [...currentIds, taskItemId],
    );
  };

  const closeEditMode = () => {
    setEditModeIsEnabled(false);
    setSelectedTaskIds([]);
    setBatchArchiveIsOpen(false);
  };

  const closeFocusedTask = useCallback(async () => {
    const idsToDelete = pendingDeletedNoteIds;

    setPendingDeletedNoteIds([]);

    for (const entryId of idsToDelete) {
      await onDeleteTimelineEntry(entryId);
    }

    onCloseTaskItem();
  }, [onCloseTaskItem, onDeleteTimelineEntry, pendingDeletedNoteIds]);

  useEffect(() => {
    if (!selectedTaskId || archiveDialogIsOpen) {
      return undefined;
    }

    const handleKeyDown = (event: globalThis.KeyboardEvent) => {
      if (event.key !== 'Escape' || isTextEditingTarget(event.target)) {
        return;
      }

      void closeFocusedTask();
    };

    window.addEventListener('keydown', handleKeyDown);

    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [archiveDialogIsOpen, closeFocusedTask, selectedTaskId]);

  return (
    <section
      className="task-board"
      aria-labelledby="task-board-title"
      data-focus-mode={Boolean(focusedTaskItem)}
    >
      {!focusedTaskItem ? (
        <WorkspaceHeader
          currentView={currentView}
          onCreateProject={onCreateProject}
          onSelectProjectFilter={(projectId) => setFilters((currentFilters) => ({
            ...currentFilters,
            category: '',
            projectId,
          }))}
          onUpdateProject={onUpdateProject}
          onUpdateWorkspace={onUpdateWorkspace}
          colorOptions={colorOptions}
          projects={projects}
          selectedProjectId={filters.projectId}
          t={t}
          workspace={workspace}
        />
      ) : null}

      {!focusedTaskItem ? (
        <TaskFilterBar
          filters={filters}
          filtersAreActive={filtersAreActive}
          onChange={setFilters}
          onReset={() => setFilters(emptyTaskWallFilters)}
          options={filterOptions}
          projects={projects}
          t={t}
        />
      ) : null}

      <div className="task-grid" aria-busy={isLoading}>
        {isLoading ? <p className="empty-copy">{t('loadingTasks')}</p> : null}
        {!isLoading && displayedTaskItems.length === 0 ? (
          <p className="empty-copy board-empty">
            {filtersAreActive
              ? t('noTasksMatch')
              : t('noTasks')}
          </p>
        ) : null}

        {displayedTaskItems.map((taskItem) => {
          const isExpanded = selectedTaskId === taskItem.id;
          const isSelectedForEdit = selectedTaskIds.includes(taskItem.id);

          return (
            <article
              className="task-card"
              data-expanded={isExpanded}
              data-edit-selected={isSelectedForEdit}
              data-edit-mode={editModeIsEnabled}
              data-state={getTaskState(taskItem)}
              key={taskItem.id}
              style={getTaskCardStyle(taskItem.color)}
            >
              {editModeIsEnabled && !isExpanded ? (
                <span
                  className="task-edit-selector"
                  aria-hidden="true"
                  data-selected={isSelectedForEdit}
                >
                  {isSelectedForEdit ? <Icon name="check" /> : null}
                </span>
              ) : null}
              <button
                aria-expanded={isExpanded}
                aria-pressed={editModeIsEnabled ? isSelectedForEdit : undefined}
                className="task-card-button"
                onClick={() => {
                  if (editModeIsEnabled) {
                    toggleSelectedTask(taskItem.id);
                    return;
                  }

                  if (isExpanded) {
                    void closeFocusedTask();
                  } else {
                    onSelectTaskItem(taskItem.id);
                  }
                }}
                title={isExpanded ? t('backToWall') : taskItem.title}
                type="button"
              >
                <span className="task-card-topline">
                  <span className="task-card-title">{taskItem.title}</span>
                  {taskItem.noteCount > 0 ? (
                    <span className="note-count">{taskItem.noteCount}</span>
                  ) : null}
                </span>
                <span className="task-card-main">
                  <span className="task-card-latest">
                    {taskItem.latestTimelineEntry ? (
                      <>
                        <span className="task-card-latest-date">
                          {formatFullDate(taskItem.latestTimelineEntry.occurredAt)}
                        </span>
                        {taskItem.latestTimelineEntry.details ?? taskItem.latestTimelineEntry.summary}
                      </>
                    ) : (
                      t('noNotesYet')
                    )}
                  </span>
                </span>
                <span className="task-card-meta">
                  {taskItem.status ? <span>{taskItem.status}</span> : null}
                  {taskItem.category ? <span>{taskItem.category}</span> : null}
                  <span title={`${t('lastUpdated')}: ${formatRelativeDate(taskItem.lastTouchedAt)}`}>
                    {formatRelativeDate(taskItem.lastTouchedAt)}
                  </span>
                  {taskItem.followUpAt ? (
                    <span
                      className="follow-up-chip"
                      data-tone={getFollowUpTone(taskItem.followUpAt)}
                    >
                      {t('followUp')} {formatFullDate(taskItem.followUpAt)}
                    </span>
                  ) : null}
                </span>
                <TaskBadges taskItem={taskItem} />
                <span
                  className="task-card-created"
                  title={`${t('created')}: ${formatFullDate(taskItem.createdAt)}`}
                >
                  {formatFullDate(taskItem.createdAt)}
                </span>
              </button>

              {isExpanded ? (
                <div className="task-card-detail">
                  {isLoadingDetail || !selectedTask ? (
                    <p className="empty-copy">Opening task...</p>
                  ) : (
                    <TaskDetail
                      archiveDialogIsOpen={archiveDialogIsOpen}
                      archiveResolutions={archiveResolutions}
                      onAddTimelineEntry={onAddTimelineEntry}
                      onArchive={onArchive}
                      onClose={closeFocusedTask}
                      onCloseArchiveDialog={onCloseArchiveDialog}
                      onOpenArchiveDialog={onOpenArchiveDialog}
                      onReopen={onReopen}
                      onQueueDeleteTimelineEntry={(entryId) =>
                        setPendingDeletedNoteIds((currentIds) =>
                          currentIds.includes(entryId) ? currentIds : [...currentIds, entryId],
                        )}
                      onUndoDeleteTimelineEntry={(entryId) =>
                        setPendingDeletedNoteIds((currentIds) =>
                          currentIds.filter((currentId) => currentId !== entryId),
                        )}
                      onUpdateFieldValues={onUpdateFieldValues}
                      onUpdateTaskItem={onUpdateTaskItem}
                      onUpdateTimelineEntry={onUpdateTimelineEntry}
                      colorOptions={colorOptions}
                      pendingDeletedNoteIds={pendingDeletedNoteIds}
                      projects={projects}
                      t={t}
                      taskItem={selectedTask}
                    />
                  )}
                </div>
              ) : null}
            </article>
          );
        })}
      </div>
      {!isLoading && canCreateTask && !focusedTaskItem ? (
        <FloatingBoardActions
          editModeIsEnabled={editModeIsEnabled}
          onCreateTaskItem={onCreateTaskItem}
          onOpenBatchArchive={() => setBatchArchiveIsOpen(true)}
          onToggleEditMode={() =>
            editModeIsEnabled ? closeEditMode() : setEditModeIsEnabled(true)}
          selectedTaskCount={selectedTaskIds.length}
          selectedProjectId={filters.projectId}
          projects={projects}
          templates={templates}
          t={t}
        />
      ) : null}
      {batchArchiveIsOpen ? (
        <ArchiveDialog
          archiveResolutions={archiveResolutions}
          onArchive={async (requestBody) => {
            await onArchiveTaskItems(selectedTaskIds, requestBody);
            closeEditMode();
          }}
          onClose={() => setBatchArchiveIsOpen(false)}
          t={t}
          taskTitle={`${selectedTaskIds.length} ${t('selectedTasks')}`}
        />
      ) : null}
    </section>
  );
}

function WorkspaceHeader({
  colorOptions,
  currentView,
  onCreateProject,
  onSelectProjectFilter,
  onUpdateProject,
  onUpdateWorkspace,
  projects,
  selectedProjectId,
  t,
  workspace,
}: {
  colorOptions: string[];
  currentView: SavedViewResponse | null;
  onCreateProject: () => void;
  onSelectProjectFilter: (projectId: string) => void;
  onUpdateProject: (id: string, requestBody: UpdateProjectRequest) => Promise<void>;
  onUpdateWorkspace: (requestBody: UpdateWorkspaceRequest) => Promise<void>;
  projects: ProjectResponse[];
  selectedProjectId: string;
  t: Translate;
  workspace: WorkspaceResponse | null;
}) {
  const [workspaceIsEditing, setWorkspaceIsEditing] = useState(false);
  const [workspaceName, setWorkspaceName] = useState(workspace?.name ?? '');
  const [workspaceColor, setWorkspaceColor] = useState(workspace?.color ?? '');
  const [editingProjectId, setEditingProjectId] = useState<string | null>(null);
  const [projectName, setProjectName] = useState('');
  const [projectColor, setProjectColor] = useState('');

  useEffect(() => {
    setWorkspaceName(workspace?.name ?? '');
    setWorkspaceColor(workspace?.color ?? '');
    setWorkspaceIsEditing(false);
  }, [workspace]);

  const startProjectEditing = (project: ProjectResponse) => {
    setEditingProjectId(project.id);
    setProjectName(project.name);
    setProjectColor(project.color ?? '');
  };

  const cancelProjectEditing = () => {
    setEditingProjectId(null);
    setProjectName('');
    setProjectColor('');
  };

  const saveWorkspace = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const trimmedName = workspaceName.trim();
    if (!trimmedName) {
      return;
    }

    await onUpdateWorkspace({
      name: trimmedName,
      color: workspaceColor.trim() || null,
    });
    setWorkspaceIsEditing(false);
  };

  const saveProject = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const trimmedName = projectName.trim();
    if (!editingProjectId || !trimmedName) {
      return;
    }

    await onUpdateProject(editingProjectId, {
      name: trimmedName,
      color: projectColor.trim() || null,
    });
    cancelProjectEditing();
  };

  return (
    <div
      className="workspace-header"
      style={getWorkspaceHeaderStyle(workspace?.color ?? null, null)}
    >
      <div className="workspace-title-block">
        <div className="workspace-title-row">
          {workspaceIsEditing ? (
            <form className="inline-heading-editor" onSubmit={(event) => void saveWorkspace(event)}>
              <input
                aria-label={t('editBoard')}
                onChange={(event) => setWorkspaceName(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === 'Escape') {
                    setWorkspaceIsEditing(false);
                    setWorkspaceName(workspace?.name ?? '');
                    setWorkspaceColor(workspace?.color ?? '');
                  }
                }}
                required
                type="text"
                value={workspaceName}
              />
              <ColorPickerPopover
                color={workspaceColor}
                colorOptions={colorOptions}
                label={t('boardColor')}
                onChange={setWorkspaceColor}
                t={t}
              />
              <button className="icon-button" title={t('saved')} type="submit">
                <Icon name="check" />
              </button>
              <button
                className="icon-button"
                onClick={() => {
                  setWorkspaceIsEditing(false);
                  setWorkspaceName(workspace?.name ?? '');
                  setWorkspaceColor(workspace?.color ?? '');
                }}
                title={t('cancel')}
                type="button"
              >
                <Icon name="close" />
              </button>
            </form>
          ) : (
            <>
              <h1 id="task-board-title">{workspace?.name ?? 'DumpTether'}</h1>
              <button
                className="icon-button header-edit-button"
                onClick={() => setWorkspaceIsEditing(true)}
                title={t('editBoard')}
                type="button"
              >
                <span
                  className="header-color-dot"
                  style={{ backgroundColor: workspace?.color ?? '#ffffff' }}
                />
                <Icon name="edit" />
              </button>
            </>
          )}
        </div>
        <div className="project-tag-strip" aria-label={t('projectTags')}>
          <button
            className="project-tag"
            data-selected={!selectedProjectId}
            onClick={() => onSelectProjectFilter('')}
            type="button"
          >
            {t('allProjects')}
          </button>
          {projects.map((project) => (
            editingProjectId === project.id ? (
              <form
                className="project-tag-editor"
                key={project.id}
                onSubmit={(event) => void saveProject(event)}
              >
                <input
                  aria-label={t('editProject')}
                  onChange={(event) => setProjectName(event.target.value)}
                  onKeyDown={(event) => {
                    if (event.key === 'Escape') {
                      cancelProjectEditing();
                    }
                  }}
                  required
                  type="text"
                  value={projectName}
                />
                <ColorPickerPopover
                  color={projectColor}
                  colorOptions={colorOptions}
                  label={`${project.name} ${t('color')}`}
                  onChange={setProjectColor}
                  t={t}
                />
                <button className="tiny-icon-button" title={t('saved')} type="submit">
                  <Icon name="check" />
                </button>
                <button
                  className="tiny-icon-button"
                  onClick={cancelProjectEditing}
                  title={t('cancel')}
                  type="button"
                >
                  <Icon name="close" />
                </button>
              </form>
            ) : (
              <span className="project-tag-wrap" key={project.id}>
                <button
                  className="project-tag"
                  data-selected={selectedProjectId === project.id}
                  onClick={() => onSelectProjectFilter(project.id)}
                  style={getContextChipStyle(project.color)}
                  title={project.name}
                  type="button"
                >
                  {project.name}
                </button>
                <button
                  className="tiny-icon-button project-tag-edit"
                  onClick={() => startProjectEditing(project)}
                  title={t('editProject')}
                  type="button"
                >
                  <Icon name="edit" />
                </button>
              </span>
            )
          ))}
          <button
            className="project-tag project-tag-add"
            onClick={onCreateProject}
            title={t('newProjectTag')}
            type="button"
          >
            <Icon name="plus" />
            <span>{t('newProjectTag')}</span>
          </button>
        </div>
        <p>{t('wallHelp')}</p>
      </div>
      <div className="board-actions">
        <span className="sort-pill">
          {t('sortedBy')} {formatSortField(currentView?.sort.field, t)}{' '}
          {currentView?.sort.direction === 'asc' ? t('sortAscending') : t('sortDescending')}
        </span>
      </div>
    </div>
  );
}

function FloatingBoardActions({
  editModeIsEnabled,
  onCreateTaskItem,
  onOpenBatchArchive,
  onToggleEditMode,
  projects,
  selectedProjectId,
  selectedTaskCount,
  templates,
  t,
}: {
  editModeIsEnabled: boolean;
  onCreateTaskItem: (title: string, options?: Partial<CreateTaskItemRequest>) => Promise<void>;
  onOpenBatchArchive: () => void;
  onToggleEditMode: () => void;
  projects: ProjectResponse[];
  selectedProjectId: string;
  selectedTaskCount: number;
  templates: TaskTemplateDetailResponse[];
  t: Translate;
}) {
  const [title, setTitle] = useState('');
  const [isOpen, setIsOpen] = useState(false);
  const [createIsOpen, setCreateIsOpen] = useState(false);
  const [selectedCreateProjectId, setSelectedCreateProjectId] = useState('');
  const [selectedTemplateId, setSelectedTemplateId] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    setSelectedCreateProjectId((currentId) => {
      if (currentId && projects.some((project) => project.id === currentId)) {
        return currentId;
      }

      if (selectedProjectId && projects.some((project) => project.id === selectedProjectId)) {
        return selectedProjectId;
      }

      return projects[0]?.id ?? '';
    });
  }, [projects, selectedProjectId]);

  useEffect(() => {
    setSelectedTemplateId((currentId) =>
      currentId && templates.some((template) => template.id === currentId)
        ? currentId
        : templates[0]?.id ?? '',
    );
  }, [templates]);

  useEffect(() => {
    if (createIsOpen) {
      inputRef.current?.focus();
    }
  }, [createIsOpen]);

  useEffect(() => {
    if (!isOpen) {
      return undefined;
    }

    const handlePointerDown = (event: PointerEvent) => {
      if (
        menuRef.current &&
        event.target instanceof Node &&
        !menuRef.current.contains(event.target)
      ) {
        setIsOpen(false);
        setCreateIsOpen(false);
      }
    };

    window.addEventListener('pointerdown', handlePointerDown);

    return () => window.removeEventListener('pointerdown', handlePointerDown);
  }, [isOpen]);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const trimmedTitle = title.trim();
    if (!trimmedTitle) {
      return;
    }

    setIsSubmitting(true);
    const selectedProject = projects.find((project) => project.id === selectedCreateProjectId);
    await onCreateTaskItem(trimmedTitle, {
      projectId: selectedProject?.id ?? null,
      category: selectedProject?.name ?? null,
      taskTemplateId: selectedTemplateId || null,
    });
    setTitle('');
    setIsSubmitting(false);
    inputRef.current?.focus();
  };

  return (
    <div className="floating-board-actions" ref={menuRef}>
      <button
        className="quick-create-fab"
        data-active={isOpen}
        onClick={() => {
          setIsOpen((open) => !open);
          setCreateIsOpen(false);
        }}
        title={t('newTask')}
        type="button"
      >
        <Icon name="plus" />
        <span>{t('newTask')}</span>
      </button>

      {isOpen && !createIsOpen ? (
        <div className="quick-action-menu">
          <button onClick={() => setCreateIsOpen(true)} type="button">
            <Icon name="plus" />
            <span>{t('addTask')}</span>
          </button>
          {editModeIsEnabled ? (
            <>
              <span className="quick-action-menu-label">
                {selectedTaskCount} {t('selectedTasks')}
              </span>
              <button
                disabled={selectedTaskCount === 0}
                onClick={() => {
                  onOpenBatchArchive();
                  setIsOpen(false);
                }}
                type="button"
              >
                <Icon name="archive" />
                <span>{t('archiveSelected')}</span>
              </button>
              <button
                className="ghost-button"
                onClick={() => {
                  onToggleEditMode();
                  setIsOpen(false);
                }}
                type="button"
              >
                <Icon name="check" />
                <span>{t('done')}</span>
              </button>
            </>
          ) : (
            <button
              onClick={() => {
                onToggleEditMode();
                setIsOpen(false);
              }}
              type="button"
            >
              <Icon name="archive" />
              <span>{t('archiveTasks')}</span>
            </button>
          )}
        </div>
      ) : null}

      {isOpen && createIsOpen ? (
        <form className="quick-create-popover" onSubmit={handleSubmit}>
          <input
            aria-label="New task title"
            ref={inputRef}
            onChange={(event) => setTitle(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === 'Escape' && !title.trim()) {
                setCreateIsOpen(false);
              }
            }}
            placeholder={t('newTaskPlaceholder')}
            type="text"
            value={title}
          />

          <select
            aria-label={t('category')}
            onChange={(event) => setSelectedCreateProjectId(event.target.value)}
            value={selectedCreateProjectId}
          >
            {projects.map((project) => (
              <option key={project.id} value={project.id}>
                {project.name}
              </option>
            ))}
          </select>

          {templates.length > 0 ? (
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
          ) : null}

          <button disabled={!title.trim() || isSubmitting} type="submit">
            <Icon name="plus" />
            <span className="sr-only">{t('addTask')}</span>
          </button>
          <button
            className="ghost-button"
            onClick={() => {
              setTitle('');
              setCreateIsOpen(false);
            }}
            title="Close"
            type="button"
          >
            <Icon name="close" />
            <span className="sr-only">Close</span>
          </button>
        </form>
      ) : null}
    </div>
  );
}

function TaskFilterBar({
  filters,
  filtersAreActive,
  onChange,
  onReset,
  options,
  projects,
  t,
}: {
  filters: TaskWallFilters;
  filtersAreActive: boolean;
  onChange: (filters: TaskWallFilters) => void;
  onReset: () => void;
  options: {
    statuses: string[];
    categories: string[];
    colors: string[];
  };
  projects: ProjectResponse[];
  t: Translate;
}) {
  const updateFilter = (update: Partial<TaskWallFilters>) => {
    onChange({ ...filters, ...update });
  };

  return (
    <div className="filter-bar" aria-label="Temporary task filters">
      <label className="filter-search">
        <span className="sr-only">Search tasks</span>
        <input
          onChange={(event) => updateFilter({ text: event.target.value })}
          placeholder={t('filterWall')}
          type="search"
          value={filters.text}
        />
      </label>

      <select
        aria-label="Filter by status"
        onChange={(event) => updateFilter({ status: event.target.value })}
        value={filters.status}
      >
        <option value="">{t('anyStatus')}</option>
        {options.statuses.map((status) => (
          <option key={status} value={status}>
            {status}
          </option>
        ))}
      </select>

      <ColorOptionPicker
        emptyLabel={t('noTaskColors')}
        label={t('color')}
        onChange={(color) => updateFilter({ color })}
        options={options.colors}
        value={filters.color}
        zeroLabel={t('anyColor')}
      />

      <select
        aria-label="Filter by category"
        onChange={(event) => updateFilter({ projectId: event.target.value })}
        value={filters.projectId}
      >
        <option value="">{t('anyProject')}</option>
        {projects.map((project) => (
          <option key={project.id} value={project.id}>
            {project.name}
          </option>
        ))}
      </select>

      <select
        aria-label="Filter by follow-up"
        onChange={(event) =>
          updateFilter({ followUp: event.target.value as '' | SavedViewFollowUpFilter })
        }
        value={filters.followUp}
      >
        <option value="">{t('anyFollowUp')}</option>
        {followUpFilters.map((filter) => (
          <option key={filter} value={filter}>
            {formatFollowUpFilter(filter)}
          </option>
        ))}
      </select>

      <input
        aria-label="Not touched for days"
        min={1}
        onChange={(event) => updateFilter({ notTouchedDays: event.target.value })}
        placeholder={t('notTouchedDays')}
        type="number"
        value={filters.notTouchedDays}
      />

      {filtersAreActive ? (
        <button
          className="icon-button reset-filters-button"
          onClick={onReset}
          title={t('removeFilters')}
          type="button"
        >
          <Icon name="filterOff" />
          <span className="sr-only">{t('removeFilters')}</span>
        </button>
      ) : null}
    </div>
  );
}

function ColorOptionPicker({
  emptyLabel,
  label,
  onChange,
  options,
  value,
  zeroLabel,
}: {
  emptyLabel: string;
  label: string;
  onChange: (color: string) => void;
  options: string[];
  value: string;
  zeroLabel: string;
}) {
  const [isOpen, setIsOpen] = useState(false);
  const pickerRef = useRef<HTMLDivElement>(null);
  const selectedColor = options.find((color) => color === value) ?? '';

  useEffect(() => {
    if (!isOpen) {
      return undefined;
    }

    const handlePointerDown = (event: PointerEvent) => {
      if (
        pickerRef.current &&
        event.target instanceof Node &&
        !pickerRef.current.contains(event.target)
      ) {
        setIsOpen(false);
      }
    };

    window.addEventListener('pointerdown', handlePointerDown);

    return () => window.removeEventListener('pointerdown', handlePointerDown);
  }, [isOpen]);

  const chooseColor = (color: string) => {
    onChange(color);
    setIsOpen(false);
  };

  return (
    <div className="color-option-picker" ref={pickerRef}>
      <button
        aria-expanded={isOpen}
        aria-label={label}
        className="color-option-trigger"
        onClick={() => setIsOpen((open) => !open)}
        type="button"
      >
        {selectedColor ? (
          <>
            <span className="color-option-swatch" style={{ backgroundColor: selectedColor }} />
            <span className="color-option-code">{selectedColor}</span>
          </>
        ) : (
          <>
            <span className="color-option-empty" />
            <span>{zeroLabel}</span>
          </>
        )}
      </button>

      {isOpen ? (
        <div className="color-option-menu" role="listbox">
          <button
            className="color-option-button"
            data-selected={!value}
            onClick={() => chooseColor('')}
            type="button"
          >
            <span className="color-option-empty" />
            <span>{zeroLabel}</span>
          </button>
          {options.map((color) => (
            <button
              className="color-option-button"
              data-selected={value.toUpperCase() === color}
              key={color}
              onClick={() => chooseColor(color)}
              title={color}
              type="button"
            >
              <span className="color-option-swatch" style={{ backgroundColor: color }} />
              <span className="color-option-code">{color}</span>
            </button>
          ))}
          {options.length === 0 ? (
            <span className="color-option-empty-text">{emptyLabel}</span>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}

function TaskDetail({
  archiveDialogIsOpen,
  archiveResolutions,
  colorOptions,
  onAddTimelineEntry,
  onArchive,
  onClose,
  onCloseArchiveDialog,
  onOpenArchiveDialog,
  onReopen,
  onQueueDeleteTimelineEntry,
  onUndoDeleteTimelineEntry,
  onUpdateFieldValues,
  onUpdateTaskItem,
  onUpdateTimelineEntry,
  pendingDeletedNoteIds,
  projects,
  t,
  taskItem,
}: {
  archiveDialogIsOpen: boolean;
  archiveResolutions: ArchiveResolutionResponse[];
  colorOptions: string[];
  onAddTimelineEntry: (note: string) => Promise<void>;
  onArchive: (requestBody: ArchiveTaskItemRequest) => Promise<void>;
  onClose: () => Promise<void>;
  onCloseArchiveDialog: () => void;
  onOpenArchiveDialog: () => void;
  onReopen: (note?: string) => Promise<void>;
  onQueueDeleteTimelineEntry: (entryId: string) => void;
  onUndoDeleteTimelineEntry: (entryId: string) => void;
  onUpdateFieldValues: (fieldValues: FieldValueMap) => Promise<void>;
  onUpdateTaskItem: (requestBody: UpdateTaskItemRequest) => Promise<void>;
  onUpdateTimelineEntry: (entryId: string, note: string) => Promise<void>;
  pendingDeletedNoteIds: string[];
  projects: ProjectResponse[];
  t: Translate;
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
  const closeFromHeader = (event: MouseEvent<HTMLDivElement>) => {
    if (
      event.target instanceof HTMLElement &&
      event.target.closest('button, input, select, textarea, label, .color-popover')
    ) {
      return;
    }

    void onClose();
  };

  return (
    <section className="task-detail" aria-label="Task detail">
      <div className="detail-header task-detail-header" onClick={closeFromHeader}>
        <button
          className="icon-button task-detail-back-button"
          onClick={() => void onClose()}
          title={t('backToWall')}
          type="button"
        >
          <Icon name="back" />
          <span className="sr-only">{t('backToWall')}</span>
        </button>
        <TaskHeaderEditor
          onUpdateTaskItem={onUpdateTaskItem}
          projects={projects}
          t={t}
          taskItem={taskItem}
        />

        <div className="detail-actions">
          {!taskItem.archivedAt ? (
            <ColorPickerPopover
              color={taskItem.color ?? ''}
              colorOptions={colorOptions}
              label={t('taskColor')}
              onChange={(color) => void onUpdateTaskItem({ color })}
              t={t}
            />
          ) : null}
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
              <span>{t('archiveAction')}</span>
            </button>
          )}
        </div>
      </div>

      <details className="detail-section fields-details">
        <summary className="section-heading">
          <span>
            <h3 id="fields-title">{t('fieldsForFiltering')}</h3>
            <small>{t('fieldsHelp')}</small>
          </span>
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
              <span>{t('saveFields')}</span>
            </button>
          ) : null}
        </summary>

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
      </details>

      <TimelinePanel
        onAddTimelineEntry={onAddTimelineEntry}
        onQueueDeleteTimelineEntry={onQueueDeleteTimelineEntry}
        onUndoDeleteTimelineEntry={onUndoDeleteTimelineEntry}
        onUpdateTimelineEntry={onUpdateTimelineEntry}
        pendingDeletedNoteIds={pendingDeletedNoteIds}
        t={t}
        timelineEntries={taskItem.timelineEntries}
      />

      {archiveDialogIsOpen ? (
        <ArchiveDialog
          archiveResolutions={archiveResolutions}
          onArchive={onArchive}
          onClose={onCloseArchiveDialog}
          t={t}
          taskTitle={taskItem.title}
        />
      ) : null}
    </section>
  );
}

function TaskHeaderEditor({
  onUpdateTaskItem,
  projects,
  t,
  taskItem,
}: {
  onUpdateTaskItem: (requestBody: UpdateTaskItemRequest) => Promise<void>;
  projects: ProjectResponse[];
  t: Translate;
  taskItem: TaskItemDetailResponse;
}) {
  const [title, setTitle] = useState(taskItem.title);
  const [status, setStatus] = useState(taskItem.status ?? '');
  const [category, setCategory] = useState(taskItem.category ?? '');
  const [categoryProjectId, setCategoryProjectId] = useState(taskItem.projectId ?? '');
  const [followUpDate, setFollowUpDate] = useState(toDateInputValue(taskItem.followUpAt));
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [saveState, setSaveState] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle');
  const [editingField, setEditingField] = useState<
    'title' | 'status' | 'category' | 'followUp' | null
  >(null);

  useEffect(() => {
    setTitle(taskItem.title);
    setStatus(taskItem.status ?? '');
    setCategory(taskItem.category ?? '');
    setCategoryProjectId(
      taskItem.projectId ??
      projects.find((project) => project.name === taskItem.category)?.id ??
      '',
    );
    setFollowUpDate(toDateInputValue(taskItem.followUpAt));
    setSaveState('idle');
  }, [projects, taskItem]);

  useEffect(() => {
    setEditingField(null);
  }, [taskItem.id]);

  const saveChanges = async (overrides: Partial<{
    title: string;
    status: string;
    category: string;
    projectId: string | null;
    followUpDate: string;
  }> = {}) => {
    if (taskItem.archivedAt) {
      return;
    }

    const nextTitle = (overrides.title ?? title).trim();
    const nextStatus = (overrides.status ?? status).trim();
    const nextCategory = (overrides.category ?? category).trim();
    const nextProjectId = Object.prototype.hasOwnProperty.call(overrides, 'projectId')
      ? overrides.projectId
      : categoryProjectId;
    const nextFollowUpDate = overrides.followUpDate ?? followUpDate;
    const normalizedFollowUpAt = nextFollowUpDate
      ? new Date(`${nextFollowUpDate}T12:00:00`).toISOString()
      : null;

    if (!nextTitle) {
      setTitle(taskItem.title);
      return;
    }

    const hasChanges =
      nextTitle !== taskItem.title ||
      nextStatus !== (taskItem.status ?? '') ||
      nextCategory !== (taskItem.category ?? '') ||
      nextProjectId !== (taskItem.projectId ?? '') ||
      normalizedFollowUpAt !== taskItem.followUpAt;

    if (!hasChanges) {
      setEditingField(null);
      return;
    }

    setSaveState('saving');
    setIsSubmitting(true);
    try {
      await onUpdateTaskItem({
        title: nextTitle,
        status: nextStatus,
        category: nextCategory,
        projectId: nextProjectId || null,
        followUpAt: normalizedFollowUpAt,
      });
      setSaveState('saved');
      setTitle(nextTitle);
      setStatus(nextStatus);
      setCategory(nextCategory);
      setCategoryProjectId(nextProjectId ?? '');
      setFollowUpDate(nextFollowUpDate);
      setEditingField(null);
    } catch {
      setSaveState('error');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleTextKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'Enter') {
      event.currentTarget.blur();
    }

    if (event.key === 'Escape') {
      setTitle(taskItem.title);
      setStatus(taskItem.status ?? '');
      setCategory(taskItem.category ?? '');
      setCategoryProjectId(taskItem.projectId ?? '');
      setFollowUpDate(toDateInputValue(taskItem.followUpAt));
      setEditingField(null);
      event.currentTarget.blur();
    }
  };

  if (taskItem.archivedAt) {
    return (
      <div className="task-header-editor">
        <p className="detail-kicker">{t('archivedTask')}</p>
        <h2>{taskItem.title}</h2>
        <div className="task-header-fields">
          <span>{t('created')}: {formatFullDate(taskItem.createdAt)}</span>
          <span title={`${t('lastUpdated')}: ${formatRelativeDate(taskItem.lastTouchedAt)}`}>
            {t('lastUpdated')}: {formatRelativeDate(taskItem.lastTouchedAt)}
          </span>
          <span>{t('status')}: {taskItem.status ?? t('noStatus')}</span>
          <span>{t('category')}: {taskItem.category ?? t('noCategory')}</span>
          <span>{t('followUpDate')}: {taskItem.followUpAt ? formatFullDate(taskItem.followUpAt) : t('noFollowUp')}</span>
        </div>
      </div>
    );
  }

  return (
    <div className="task-header-editor">
      <p className="detail-kicker">{t('activeTask')}</p>
      <div className="task-title-row task-title-display-row">
        {editingField === 'title' ? (
          <input
            aria-label={t('editTask')}
            className="task-title-input"
            disabled={isSubmitting}
            onBlur={() => void saveChanges()}
            onChange={(event) => setTitle(event.target.value)}
            onKeyDown={handleTextKeyDown}
            required
            type="text"
            value={title}
          />
        ) : (
          <h2>{taskItem.title}</h2>
        )}
        <button
          className="icon-button header-edit-button"
          onClick={(event) => {
            event.stopPropagation();
            setEditingField('title');
          }}
          title={t('editTask')}
          type="button"
        >
          <Icon name="edit" />
        </button>
      </div>
      <div className="task-header-fields">
        <span>{t('created')}: {formatFullDate(taskItem.createdAt)}</span>
        <span title={`${t('lastUpdated')}: ${formatRelativeDate(taskItem.lastTouchedAt)}`}>
          {t('lastUpdated')}: {formatRelativeDate(taskItem.lastTouchedAt)}
        </span>
        {editingField === 'status' ? (
          <input
            aria-label={t('status')}
            autoFocus
            disabled={isSubmitting}
            onBlur={() => void saveChanges()}
            onChange={(event) => setStatus(event.target.value)}
            onKeyDown={handleTextKeyDown}
            placeholder={t('noStatus')}
            type="text"
            value={status}
          />
        ) : (
          <button
            className="task-meta-chip"
            onClick={() => setEditingField('status')}
            type="button"
          >
            {t('status')}: {taskItem.status ?? t('noStatus')}
          </button>
        )}
        {editingField === 'category' ? (
          <select
            aria-label={t('category')}
            autoFocus
            disabled={isSubmitting}
            onBlur={() => void saveChanges()}
            onChange={(event) => {
              const project = projects.find((candidate) => candidate.id === event.target.value);
              const nextCategory = project?.name ?? '';
              setCategoryProjectId(project?.id ?? '');
              setCategory(nextCategory);
              void saveChanges({
                category: nextCategory,
                projectId: project?.id ?? null,
              });
            }}
            value={categoryProjectId}
          >
            <option value="">{t('noCategory')}</option>
            {projects.map((project) => (
              <option key={project.id} value={project.id}>
                {project.name}
              </option>
            ))}
          </select>
        ) : (
          <button
            className="task-meta-chip"
            onClick={() => setEditingField('category')}
            type="button"
          >
            {t('category')}: {taskItem.category ?? t('noCategory')}
          </button>
        )}
        {editingField === 'followUp' ? (
          <input
            aria-label={t('followUpDate')}
            autoFocus
            disabled={isSubmitting}
            onBlur={() => void saveChanges()}
            onChange={(event) => {
              setFollowUpDate(event.target.value);
              void saveChanges({ followUpDate: event.target.value });
            }}
            type="date"
            value={followUpDate}
          />
        ) : (
          <button
            className="task-meta-chip follow-up-chip"
            data-tone={getFollowUpTone(taskItem.followUpAt)}
            onClick={() => setEditingField('followUp')}
            type="button"
          >
            {t('followUpDate')}: {taskItem.followUpAt ? formatFullDate(taskItem.followUpAt) : t('noFollowUp')}
          </button>
        )}
        {saveState !== 'idle' ? (
          <span className="saving-copy" data-state={saveState}>
            {saveState === 'saving'
              ? t('saving')
              : saveState === 'saved'
                ? t('saved')
                : t('saveFailed')}
          </span>
        ) : null}
      </div>
    </div>
  );
}

function ColorPickerPopover({
  color,
  colorOptions,
  label,
  onChange,
  t,
}: {
  color: string;
  colorOptions?: string[];
  label: string;
  onChange: (color: string) => void;
  t: Translate;
}) {
  const [isOpen, setIsOpen] = useState(false);
  const popoverRef = useRef<HTMLDivElement>(null);
  const selectedColor = isHexColor(color) ? color : '#FDE68A';
  const choices = useMemo(
    () => mergeColorOptions(colorOptions ?? [], colorChoices, color ? [color] : []),
    [color, colorOptions],
  );

  useEffect(() => {
    if (!isOpen) {
      return undefined;
    }

    const handlePointerDown = (event: PointerEvent) => {
      if (
        popoverRef.current &&
        event.target instanceof Node &&
        !popoverRef.current.contains(event.target)
      ) {
        setIsOpen(false);
      }
    };

    window.addEventListener('pointerdown', handlePointerDown);

    return () => window.removeEventListener('pointerdown', handlePointerDown);
  }, [isOpen]);

  const chooseColor = (nextColor: string) => {
    onChange(nextColor);
    setIsOpen(false);
  };

  return (
    <div className="color-popover" ref={popoverRef}>
      <button
        aria-expanded={isOpen}
        aria-label={label}
        className="color-trigger"
        onClick={(event) => {
          event.stopPropagation();
          setIsOpen((open) => !open);
        }}
        style={{ '--picker-color': color || '#FFFFFF' } as CSSProperties}
        title={label}
        type="button"
      >
        <span className="color-trigger-dot" />
        <Icon name="edit" />
      </button>
      {isOpen ? (
        <div className="color-popover-panel">
          <div className="color-swatch-row" aria-label={label}>
            {choices.map((choice) => (
              <button
                aria-label={`Use ${choice}`}
                className="color-swatch"
                data-selected={color.toUpperCase() === choice}
                key={choice}
                onClick={() => chooseColor(choice)}
                style={{ backgroundColor: choice }}
                type="button"
              />
            ))}
            <span className="color-popover-code">{color || t('noColor')}</span>
            <input
              aria-label="Custom color"
              onChange={(event) => chooseColor(event.target.value.toUpperCase())}
              type="color"
              value={selectedColor}
            />
          </div>
          {color ? (
            <button
              className="clear-color-button"
              onClick={() => chooseColor('')}
              type="button"
            >
              {t('clearColor')}
            </button>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}

function TemplatesPage({
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
  ) => Promise<void>;
  t: Translate;
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
            <h1 id="templates-title">{t('templates')}</h1>
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
                title="Move up"
                type="button"
              >
                <Icon name="arrowUp" />
                <span className="sr-only">Move up</span>
              </button>
              <button
                disabled={index === fields.length - 1}
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
  colorOptions,
  onClose,
  onDeleteView,
  onSaveView,
  projects,
  savedView,
  t,
}: {
  colorOptions: string[];
  onClose: () => void;
  onDeleteView: (id: string) => Promise<void>;
  onSaveView: (
    id: string | null,
    requestBody: Parameters<typeof createSavedView>[0],
  ) => Promise<void>;
  projects: ProjectResponse[];
  savedView: SavedViewResponse | null;
  t: Translate;
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
              Category
              <input
                onChange={(event) => updateDraft({ category: event.target.value })}
                placeholder="Procurement"
                type="text"
                value={draft.category}
              />
            </label>

            <div className="view-color-filter">
              <span className="field-label">{t('color')}</span>
              <ColorOptionPicker
                emptyLabel={t('noTaskColors')}
                label={t('color')}
                onChange={(color) => updateDraft({ color })}
                options={colorOptions}
                value={draft.color}
                zeroLabel={t('anyColor')}
              />
            </div>

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
                    {formatSortField(field, t)}
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
                    {direction === 'asc' ? t('sortAscending') : t('sortDescending')}
                  </option>
                ))}
              </select>
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

function TimelinePanel({
  onAddTimelineEntry,
  onQueueDeleteTimelineEntry,
  onUndoDeleteTimelineEntry,
  onUpdateTimelineEntry,
  pendingDeletedNoteIds,
  t,
  timelineEntries,
}: {
  onAddTimelineEntry: (note: string) => Promise<void>;
  onQueueDeleteTimelineEntry: (entryId: string) => void;
  onUndoDeleteTimelineEntry: (entryId: string) => void;
  onUpdateTimelineEntry: (entryId: string, note: string) => Promise<void>;
  pendingDeletedNoteIds: string[];
  t: Translate;
  timelineEntries: TaskItemDetailResponse['timelineEntries'];
}) {
  const notes = timelineEntries.filter((entry) => entry.kind === 'NoteAdded');

  return (
    <section className="timeline-panel notes-panel" aria-labelledby="timeline-title">
      <div className="section-heading">
        <h3 id="timeline-title">{t('notes')}</h3>
        <span>{notes.length} {t('noteCount')}</span>
      </div>

      <AddTimelineEntryForm onAddTimelineEntry={onAddTimelineEntry} t={t} />

      <ol className="timeline-list">
        {notes.length === 0 ? <li className="empty-copy">{t('noNotesYet')}</li> : null}
        {notes.map((entry) => (
          <NoteEntry
            entry={entry}
            isPendingDelete={pendingDeletedNoteIds.includes(entry.id)}
            key={entry.id}
            onQueueDeleteTimelineEntry={onQueueDeleteTimelineEntry}
            onUndoDeleteTimelineEntry={onUndoDeleteTimelineEntry}
            onUpdateTimelineEntry={onUpdateTimelineEntry}
            t={t}
          />
        ))}
      </ol>
    </section>
  );
}

function NoteEntry({
  entry,
  isPendingDelete,
  onQueueDeleteTimelineEntry,
  onUndoDeleteTimelineEntry,
  onUpdateTimelineEntry,
  t,
}: {
  entry: TaskItemDetailResponse['timelineEntries'][number];
  isPendingDelete: boolean;
  onQueueDeleteTimelineEntry: (entryId: string) => void;
  onUndoDeleteTimelineEntry: (entryId: string) => void;
  onUpdateTimelineEntry: (entryId: string, note: string) => Promise<void>;
  t: Translate;
}) {
  const [isEditing, setIsEditing] = useState(false);
  const [draft, setDraft] = useState(entry.details ?? '');
  const [isConfirmingDelete, setIsConfirmingDelete] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    setDraft(entry.details ?? '');
    setIsEditing(false);
    setIsConfirmingDelete(false);
  }, [entry]);

  const save = async () => {
    const trimmedDraft = draft.trim();
    if (!trimmedDraft) {
      return;
    }

    setIsSubmitting(true);
    await onUpdateTimelineEntry(entry.id, trimmedDraft);
    setIsSubmitting(false);
    setIsEditing(false);
  };

  return (
    <li className="note-entry" data-pending-delete={isPendingDelete}>
      <time dateTime={entry.occurredAt}>{formatDateTime(entry.occurredAt)}</time>
      {isEditing ? (
        <div className="note-edit">
          <textarea
            aria-label={t('note')}
            onChange={(event) => setDraft(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === 'Enter' && !event.shiftKey) {
                event.preventDefault();
                void save();
              }
            }}
            rows={3}
            value={draft}
          />
          <div className="note-actions">
            <button disabled={!draft.trim() || isSubmitting} onClick={() => void save()} type="button">
              {t('save')}
            </button>
            <button className="ghost-button" onClick={() => setIsEditing(false)} type="button">
              {t('cancel')}
            </button>
          </div>
        </div>
      ) : (
        <button
          className="note-body"
          onClick={() => setIsEditing(true)}
          type="button"
        >
          {entry.details}
        </button>
      )}
      <div className="note-delete-cell">
        {isPendingDelete ? (
          <button
            className="ghost-button note-undo-button"
            onClick={() => onUndoDeleteTimelineEntry(entry.id)}
            type="button"
          >
            {t('undo')}
          </button>
        ) : isConfirmingDelete ? (
          <div className="note-confirm-delete">
            <span>{t('confirmDelete')}</span>
            <button
              className="icon-button"
              onClick={() => {
                onQueueDeleteTimelineEntry(entry.id);
                setIsConfirmingDelete(false);
              }}
              title={t('deleteNote')}
              type="button"
            >
              <Icon name="close" />
            </button>
            <button
              className="ghost-button"
              onClick={() => setIsConfirmingDelete(false)}
              type="button"
            >
              {t('keep')}
            </button>
          </div>
        ) : (
          <button
            className="icon-button note-delete-button"
            onClick={() => setIsConfirmingDelete(true)}
            title={t('deleteNote')}
            type="button"
          >
            <Icon name="close" />
          </button>
        )}
      </div>
    </li>
  );
}

function AddTimelineEntryForm({
  onAddTimelineEntry,
  t,
}: {
  onAddTimelineEntry: (note: string) => Promise<void>;
  t: Translate;
}) {
  const [note, setNote] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

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
    textareaRef.current?.focus();
  };

  return (
    <form className="timeline-form" onSubmit={handleSubmit}>
      <textarea
        aria-label={t('note')}
        ref={textareaRef}
        onChange={(event) => setNote(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === 'Enter' && !event.shiftKey) {
            event.preventDefault();
            event.currentTarget.form?.requestSubmit();
          }
        }}
        placeholder={t('addNotePlaceholder')}
        rows={3}
        value={note}
      />
      <button disabled={!note.trim() || isSubmitting} type="submit">
        <Icon name="note" />
        <span>{t('note')}</span>
      </button>
    </form>
  );
}

function ArchiveDialog({
  archiveResolutions,
  onArchive,
  onClose,
  t,
  taskTitle,
}: {
  archiveResolutions: ArchiveResolutionResponse[];
  onArchive: (requestBody: ArchiveTaskItemRequest) => Promise<void>;
  onClose: () => void;
  t: Translate;
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
            <p className="detail-kicker">{t('archiveAction')}</p>
            <h2 id="archive-dialog-title">{taskTitle}</h2>
          </div>
          <button className="icon-button" onClick={onClose} type="button">
            <Icon name="close" />
            <span className="sr-only">{t('cancel')}</span>
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
              {t('cancel')}
            </button>
            <button disabled={!canSubmit || isSubmitting} type="submit">
              {t('archiveAction')}
            </button>
          </div>
        </form>
      </section>
    </div>
  );
}

function SettingsPanel({
  language,
  onChangeLanguage,
  onClose,
  t,
}: {
  language: Language;
  onChangeLanguage: (language: Language) => void;
  onClose: () => void;
  t: Translate;
}) {
  return (
    <div className="dialog-backdrop" role="presentation">
      <section
        aria-labelledby="settings-title"
        aria-modal="true"
        className="settings-panel"
        role="dialog"
      >
        <div className="dialog-header">
          <div>
            <p className="detail-kicker">DumpTether</p>
            <h2 id="settings-title">{t('settings')}</h2>
          </div>
          <button className="icon-button" onClick={onClose} type="button">
            <Icon name="close" />
            <span className="sr-only">Close settings</span>
          </button>
        </div>

        <label>
          {t('language')}
          <select
            onChange={(event) => onChangeLanguage(event.target.value as Language)}
            value={language}
          >
            <option value="en">{t('english')}</option>
            <option value="da">{t('danish')}</option>
          </select>
        </label>

        <div className="settings-section">
          <h3>{t('cleanup')}</h3>
          <p>{t('cleanupFuture')}</p>
          <div className="settings-action-grid">
            <button disabled type="button">{t('clearArchive')}</button>
            <button disabled type="button">{t('clearOldTasks')}</button>
            <button disabled type="button">{t('clearWorkspaceTasks')}</button>
            <button disabled type="button">{t('deleteProjectTag')}</button>
            <button disabled type="button">{t('deleteBoard')}</button>
          </div>
        </div>
      </section>
    </div>
  );
}

function TaskBadges({ taskItem }: { taskItem: TaskItemSummaryResponse }) {
  const badges = getTaskBadges(taskItem);

  if (badges.length === 0) {
    return null;
  }

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
    arrowDown: 'M12 5v14m0 0 6-6m-6 6-6-6',
    arrowUp: 'M12 19V5m0 0 6 6m-6-6-6 6',
    back: 'M15 6 9 12l6 6M10 12h10',
    check: 'm5 13 4 4L19 7',
    clock: 'M12 4a8 8 0 1 0 0 16 8 8 0 0 0 0-16Zm0 4v5l3 2',
    close: 'M6 6l12 12M18 6 6 18',
    edit: 'M4 20h4l10-10-4-4L4 16v4Zm12-16 4 4',
    filterOff: 'M4 5h16l-6 7v3l-4 2v-5L4 5Zm3 15 13-13',
    inbox: 'M4 5h16v10l-3 4H7l-3-4V5Zm0 10h5l1.5 2h3L15 15h5',
    list: 'M8 6h12M8 12h12M8 18h12M4 6h.01M4 12h.01M4 18h.01',
    note: 'M5 4h11l3 3v13H5V4Zm11 0v4h4M8 12h8M8 16h6',
    palette: 'M12 4a8 8 0 0 0-1 15.94c.8.1 1.33-.55 1.14-1.33-.13-.55.28-1.04.85-1.04h1.36A5.65 5.65 0 0 0 20 11.92C20 7.55 16.42 4 12 4ZM8 11.5h.01M10 8h.01M14 8h.01M16 11h.01',
    panel: 'M4 5h16v14H4V5Zm5 0v14',
    plus: 'M12 5v14M5 12h14',
    refresh: 'M20 7v5h-5M4 17v-5h5M18 10a6 6 0 0 0-10-4L4 10m2 4a6 6 0 0 0 10 4l4-4',
    search: 'M11 4a7 7 0 1 0 0 14 7 7 0 0 0 0-14Zm5 12 4 4',
    settings: 'M12 8.5a3.5 3.5 0 1 0 0 7 3.5 3.5 0 0 0 0-7Zm8 3.5-2.1-.6a6.9 6.9 0 0 0-.7-1.7l1.1-1.9-2.1-2.1-1.9 1.1a6.9 6.9 0 0 0-1.7-.7L12 4H9l-.6 2.1a6.9 6.9 0 0 0-1.7.7L4.8 5.7 2.7 7.8l1.1 1.9a6.9 6.9 0 0 0-.7 1.7L1 12l.6 3 2.1.6c.2.6.4 1.2.7 1.7l-1.1 1.9 2.1 2.1 1.9-1.1c.5.3 1.1.5 1.7.7L9 23h3l.6-2.1c.6-.2 1.2-.4 1.7-.7l1.9 1.1 2.1-2.1-1.1-1.9c.3-.5.5-1.1.7-1.7L20 15l.6-3Z',
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

  return findViewId(views, 'All Tasks') ?? findViewId(views, 'Overview') ?? views[0]?.id ?? null;
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

function getInitialLanguage(): Language {
  const storedLanguage = window.localStorage.getItem(languageStorageKey);
  return storedLanguage === 'da' ? 'da' : 'en';
}

function getInitialWorkspaceId() {
  const storedWorkspaceId = window.localStorage.getItem(workspaceStorageKey);
  return storedWorkspaceId && storedWorkspaceId.length > 0 ? storedWorkspaceId : null;
}

function translate(language: Language, key: TranslationKey) {
  return translations[language][key] ?? translations.en[key];
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

const emptyTaskWallFilters: TaskWallFilters = {
  text: '',
  status: '',
  category: '',
  color: '',
  projectId: '',
  notTouchedDays: '',
  followUp: '',
};

function buildTaskFilterOptions(
  taskItems: TaskItemSummaryResponse[],
  colorOptions: string[],
) {
  return {
    statuses: uniqueSorted(taskItems.map((taskItem) => taskItem.status)),
    categories: uniqueSorted(taskItems.map((taskItem) => taskItem.category)),
    colors: colorOptions,
  };
}

function getTaskColors(taskItems: TaskItemSummaryResponse[]) {
  return taskItems
    .map((taskItem) => taskItem.color)
    .filter((color): color is string => Boolean(color));
}

function mergeColorOptions(...sources: string[][]) {
  return Array.from(
    new Set(
      sources
        .flat()
        .map((color) => color.trim().toUpperCase())
        .filter(isHexColor),
    ),
  );
}

function uniqueSorted(values: Array<string | null>) {
  return Array.from(
    new Set(values.filter((value): value is string => Boolean(value))),
  ).sort((left, right) => left.localeCompare(right));
}

function applyTaskWallFilters(
  taskItems: TaskItemSummaryResponse[],
  filters: TaskWallFilters,
) {
  const text = filters.text.trim().toLowerCase();
  const notTouchedDays = numberOrNull(filters.notTouchedDays);

  return taskItems.filter((taskItem) => {
    if (text && !taskMatchesText(taskItem, text)) {
      return false;
    }

    if (filters.status && taskItem.status !== filters.status) {
      return false;
    }

    if (filters.category && taskItem.category !== filters.category) {
      return false;
    }

    if (filters.color && taskItem.color !== filters.color) {
      return false;
    }

    if (filters.projectId && taskItem.projectId !== filters.projectId) {
      return false;
    }

    if (filters.followUp && !taskMatchesFollowUp(taskItem, filters.followUp)) {
      return false;
    }

    if (notTouchedDays && !isNotTouchedForDays(taskItem, notTouchedDays)) {
      return false;
    }

    return true;
  });
}

function taskMatchesText(taskItem: TaskItemSummaryResponse, text: string) {
  return [
    taskItem.title,
    taskItem.status,
    taskItem.category,
    taskItem.latestTimelineEntry?.details,
  ].some((value) => value?.toLowerCase().includes(text));
}

function taskMatchesFollowUp(
  taskItem: TaskItemSummaryResponse,
  followUp: SavedViewFollowUpFilter,
) {
  if (!taskItem.followUpAt) {
    return false;
  }

  const followUpAt = new Date(taskItem.followUpAt);
  const now = new Date();
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const tomorrow = new Date(today);
  tomorrow.setDate(today.getDate() + 1);
  const nextWeek = new Date(today);
  nextWeek.setDate(today.getDate() + 7);

  switch (followUp) {
    case 'Overdue':
      return getFollowUpTone(taskItem.followUpAt) === 'overdue';
    case 'Today':
      return followUpAt >= today && followUpAt < tomorrow;
    case 'ThisWeek':
      return followUpAt >= today && followUpAt < nextWeek;
    case 'Any':
    default:
      return true;
  }
}

function isNotTouchedForDays(taskItem: TaskItemSummaryResponse, days: number) {
  const threshold = Date.now() - days * 24 * 60 * 60 * 1000;
  return new Date(taskItem.lastTouchedAt).getTime() <= threshold;
}

function taskWallFiltersAreActive(filters: TaskWallFilters) {
  return Object.values(filters).some((value) => value.trim().length > 0);
}

function getTaskCardStyle(color: string | null) {
  const taskColor = color && isHexColor(color) ? color : '#FFF3A6';
  const textColor = getReadableTextColor(taskColor);

  return {
    '--task-note-color': taskColor,
    '--task-note-text': textColor,
    '--task-note-chip-bg':
      textColor === '#FFFFFF'
        ? 'rgba(255, 255, 255, 0.16)'
        : 'rgba(255, 255, 255, 0.46)',
    '--task-note-chip-border':
      textColor === '#FFFFFF'
        ? 'rgba(255, 255, 255, 0.24)'
        : 'rgba(24, 33, 44, 0.1)',
  } as CSSProperties;
}

function getWorkspaceHeaderStyle(
  workspaceColor: string | null,
  projectColor: string | null,
) {
  const baseColor = workspaceColor && isHexColor(workspaceColor)
    ? workspaceColor
    : '#E8F3F0';
  const accentColor = projectColor && isHexColor(projectColor)
    ? projectColor
    : baseColor;

  return {
    '--workspace-color': baseColor,
    '--project-color': accentColor,
    '--workspace-text': getReadableTextColor(baseColor),
  } as CSSProperties;
}

function getSidebarStyle(workspaceColor: string | null) {
  const baseColor = workspaceColor && isHexColor(workspaceColor)
    ? workspaceColor
    : '#184C48';

  return {
    '--sidebar-workspace-color': baseColor,
    '--sidebar-workspace-text': getReadableTextColor(baseColor),
  } as CSSProperties;
}

function getContextChipStyle(color: string | null) {
  if (!color || !isHexColor(color)) {
    return undefined;
  }

  return {
    '--context-chip-color': color,
    '--context-chip-text': getReadableTextColor(color),
  } as CSSProperties;
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

  return badges;
}

function isTextEditingTarget(target: EventTarget | null) {
  if (!(target instanceof HTMLElement)) {
    return false;
  }

  const tagName = target.tagName.toLowerCase();
  return (
    tagName === 'input' ||
    tagName === 'textarea' ||
    tagName === 'select' ||
    target.isContentEditable
  );
}

function isHexColor(value: string) {
  return /^#[0-9A-F]{6}$/i.test(value);
}

function getReadableTextColor(hexColor: string) {
  const red = Number.parseInt(hexColor.slice(1, 3), 16);
  const green = Number.parseInt(hexColor.slice(3, 5), 16);
  const blue = Number.parseInt(hexColor.slice(5, 7), 16);
  const luminance = (0.2126 * red + 0.7152 * green + 0.0722 * blue) / 255;

  return luminance > 0.55 ? '#18212C' : '#FFFFFF';
}

function isWaiting(taskItem: TaskItemSummaryResponse) {
  return taskItem.status?.toLowerCase().includes('waiting') ?? false;
}

function isFollowUpOverdue(taskItem: TaskItemSummaryResponse) {
  return Boolean(taskItem.followUpAt) &&
    !taskItem.archivedAt &&
    getFollowUpTone(taskItem.followUpAt) === 'overdue';
}

function getFollowUpTone(value: string | null) {
  if (!value) {
    return 'none';
  }

  const followUpDate = new Date(value);
  const now = new Date();
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const tomorrow = new Date(today);
  tomorrow.setDate(today.getDate() + 1);
  const followUpDay = new Date(
    followUpDate.getFullYear(),
    followUpDate.getMonth(),
    followUpDate.getDate(),
  );

  if (followUpDay.getTime() < today.getTime()) {
    return 'overdue';
  }

  if (followUpDay.getTime() < tomorrow.getTime()) {
    return 'today';
  }

  return 'future';
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
    category: savedView?.filter.category ?? '',
    color: savedView?.filter.color ?? '',
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
      category: draft.category.trim() || null,
      color: draft.color || null,
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

function formatSortField(value: SavedViewSortField | null | undefined, t: Translate) {
  switch (value) {
    case 'createdAt':
      return t('sortCreated');
    case 'followUpAt':
      return t('sortFollowUp');
    case 'title':
      return t('sortTitle');
    case 'status':
      return t('sortStatus');
    case 'lastTouchedAt':
    default:
      return t('sortLastTouched');
  }
}

function formatSavedViewName(name: string, t: Translate) {
  switch (name.toLowerCase()) {
    case 'overview':
    case 'all tasks':
      return t('overview');
    case 'archive':
      return t('archive');
    default:
      return name;
  }
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value));
}

function formatFullDate(value: string) {
  return new Intl.DateTimeFormat(undefined, {
    year: 'numeric',
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
