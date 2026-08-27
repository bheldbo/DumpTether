import {
  type CSSProperties,
  type PointerEvent as ReactPointerEvent,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import { Icon } from '../../components/Icon';
import { TaskFilterBar } from '../../components/TaskFilterBar';
import { TaskBadges, TaskMetaChip } from '../../components/TaskMetadata';
import { TaskSyncIndicator } from '../../components/TaskSyncIndicator';
import { type ToastTone } from '../../appTypes';
import {
  formatFullDate,
  formatRelativeDate,
  formatWorkspaceName,
  isOwnerRole,
  isReadOnlyRole,
  isSystemAllTasksWorkspace,
  isTaskShareWorkspace,
  isTextEditingTarget,
} from '../../appUtils';
import { type Translate } from '../../localization';
import {
  applyTaskWallFilters,
  buildTaskFilterOptions,
  emptyTaskWallFilters,
  getContextChipStyle,
  getFollowUpTone,
  getTaskCardStyle,
  getTaskState,
  splitTaskCategories,
  taskWallFiltersAreActive,
  type TaskWallFilters,
  uniqueSorted,
} from '../../taskUtils';
import type {
  CloudSyncAccountResponse,
  CreateTaskShareLinkRequest,
  CreateTaskShareRequest,
  CreateTaskItemRequest,
  CreateWorkspaceInvitationRequest,
  FieldValueMap,
  ProjectResponse,
  SavedViewResponse,
  SavedViewSort,
  TaskItemDetailResponse,
  TaskItemShareRole,
  TaskItemSummaryResponse,
  TaskShareLinkResponse,
  TaskTemplateDetailResponse,
  SyncRootResponse,
  SyncWorkspaceWithCloudRequest,
  SyncWorkspaceWithCloudResponse,
  WorkspaceInvitationResponse,
  WorkspaceMemberResponse,
  UpdateProjectRequest,
  UpdateTaskItemRequest,
  UpdateTaskShareRequest,
  UpdateWorkspaceMemberRequest,
  UpdateWorkspaceRequest,
  WorkspaceResponse,
} from '../../types';
import { type CreateTaskItemOptions } from './taskWallTypes';
import { PermanentDeleteDialog, ReopenDialog } from '../task-detail/TaskDialogs';
import { TaskDetail } from '../task-detail/TaskDetail';
import { ShareDialog } from '../sharing/ShareDialog';
import { BoardLoadingState } from './BoardLoadingState';
import { DraftTaskCard } from './DraftTaskCard';
import { FloatingBoardActions } from './FloatingBoardActions';
import { WorkspaceHeader } from './WorkspaceHeader';
import { CloudSyncDialog } from '../sync/CloudSyncDialog';

interface DraftTaskTarget {
  workspaceId: string;
  workspaceName: string;
  workspaceColor: string | null;
  projects: ProjectResponse[];
  selectedProjectId: string;
}
export function TaskBoard({
  colorOptions,
  currentView,
  currentUserEmail,
  cloudSyncAccount,
  isLoading,
  isLoadingDetail,
  isRefreshing,
  localDesktopSessionIsActive,
  onAddTimelineEntry,
  onArchive,
  onArchiveTaskItems,
  onBeforeTaskNavigation,
  onCopyTaskItemsToWorkspace,
  onCreateProject,
  onCreateTaskShareLink,
  onCreateTaskShareLinks,
  onCreateTaskItem,
  onCreateSubtask,
  onCreateWorkspaceInvitation,
  onDeleteProject,
  onDeleteSubtask,
  onDeleteTimelineEntry,
  onImportTaskTemplate,
  onListSubtasks,
  onReopen,
  onReopenTaskItems,
  onDeleteTaskItemsPermanently,
  onRemoveWorkspaceMember,
  onRevokeTaskShare,
  onRevokeWorkspaceInvitation,
  onCloseTaskItem,
  onSelectTaskItem,
  onUnsavedChangesChange,
  onUpdateFieldValues,
  onUpdateProject,
  onSyncWorkspaceWithCloud,
  onUpdateTaskShareRole,
  onUpdateTaskItems,
  onUpdateTaskItem,
  onUpdateTimelineEntry,
  onUpdateWorkspace,
  onUpdateWorkspaceMemberRole,
  onShowToast,
  projects,
  selectedTask,
  selectedTaskId,
  statusOptions,
  statusColors,
  syncRoot,
  taskItems,
  templates,
  importedTemplateSourceIds,
  t,
  workspaceInvitations,
  workspaceMembers,
  workspace,
  workspaces,
}: {
  colorOptions: string[];
  currentView: SavedViewResponse | null;
  currentUserEmail: string | null;
  cloudSyncAccount: CloudSyncAccountResponse | null;
  isLoading: boolean;
  isLoadingDetail: boolean;
  isRefreshing: boolean;
  localDesktopSessionIsActive: boolean;
  onAddTimelineEntry: (note: string, fieldValues?: FieldValueMap) => Promise<void>;
  onArchive: () => Promise<void>;
  onArchiveTaskItems: (taskItemIds: string[]) => Promise<void>;
  onBeforeTaskNavigation: () => boolean;
  onCopyTaskItemsToWorkspace: (taskItemIds: string[], workspaceId: string) => Promise<void>;
  onCreateProject: (name: string, color?: string | null) => Promise<void>;
  onCreateTaskShareLink: (
    taskItemId: string,
    requestBody: CreateTaskShareRequest,
  ) => Promise<TaskShareLinkResponse>;
  onCreateTaskShareLinks: (
    requestBody: CreateTaskShareLinkRequest,
  ) => Promise<TaskShareLinkResponse>;
  onCreateTaskItem: (
    title: string,
    options?: CreateTaskItemOptions,
  ) => Promise<TaskItemDetailResponse | null>;
  onCreateSubtask: (
    parentTaskItem: TaskItemDetailResponse,
    requestBody: CreateTaskItemRequest,
  ) => Promise<TaskItemDetailResponse>;
  onListSubtasks: (
    parentTaskItem: TaskItemDetailResponse,
  ) => Promise<TaskItemSummaryResponse[]>;
  onCreateWorkspaceInvitation: (
    requestBody: CreateWorkspaceInvitationRequest,
  ) => Promise<WorkspaceInvitationResponse>;
  onDeleteProject: (projectId: string) => Promise<void>;
  onDeleteSubtask: (parentTaskItem: TaskItemSummaryResponse, subtaskId: string) => Promise<void>;
  onDeleteTimelineEntry: (entryId: string) => Promise<void>;
  onImportTaskTemplate: (taskItemId: string) => Promise<void>;
  onReopen: (note?: string) => Promise<void>;
  onReopenTaskItems: (taskItemIds: string[], note?: string) => Promise<void>;
  onDeleteTaskItemsPermanently: (taskItemIds: string[]) => Promise<void>;
  onRemoveWorkspaceMember: (userId: string) => Promise<void>;
  onRevokeTaskShare: (taskItemId: string, shareId: string) => Promise<void>;
  onRevokeWorkspaceInvitation: (id: string) => Promise<void>;
  onCloseTaskItem: (destination?: 'default' | 'wall' | 'parent') => void;
  onSelectTaskItem: (
    id: string,
    workspaceId: string,
    returnTarget?: 'wall' | 'parent',
  ) => void;
  onUnsavedChangesChange: (hasUnsavedChanges: boolean) => void;
  onUpdateFieldValues: (fieldValues: FieldValueMap) => Promise<void>;
  onUpdateProject: (id: string, requestBody: UpdateProjectRequest) => Promise<void>;
  onSyncWorkspaceWithCloud: (
    workspaceId: string,
    requestBody: SyncWorkspaceWithCloudRequest,
  ) => Promise<SyncWorkspaceWithCloudResponse>;
  onUpdateTaskShareRole: (
    taskItemId: string,
    shareId: string,
    requestBody: UpdateTaskShareRequest,
  ) => Promise<TaskItemDetailResponse>;
  onUpdateTaskItems: (taskItemIds: string[], requestBody: UpdateTaskItemRequest) => Promise<void>;
  onUpdateTaskItem: (requestBody: UpdateTaskItemRequest) => Promise<void>;
  onUpdateTimelineEntry: (
    entryId: string,
    note: string | null,
    fieldValues?: FieldValueMap,
  ) => Promise<void>;
  onUpdateWorkspace: (requestBody: UpdateWorkspaceRequest) => Promise<void>;
  onUpdateWorkspaceMemberRole: (
    userId: string,
    requestBody: UpdateWorkspaceMemberRequest,
  ) => Promise<WorkspaceMemberResponse>;
  onShowToast: (message: string, tone?: ToastTone) => void;
  projects: ProjectResponse[];
  selectedTask: TaskItemDetailResponse | null;
  selectedTaskId: string | null;
  statusOptions: string[];
  statusColors: Record<string, string>;
  syncRoot: SyncRootResponse | null;
  taskItems: TaskItemSummaryResponse[];
  templates: TaskTemplateDetailResponse[];
  importedTemplateSourceIds: string[];
  t: Translate;
  workspaceInvitations: WorkspaceInvitationResponse[];
  workspaceMembers: WorkspaceMemberResponse[];
  workspace: WorkspaceResponse | null;
  workspaces: WorkspaceResponse[];
}) {
  const currentWorkspaceMember = currentUserEmail
    ? workspaceMembers.find((member) =>
        member.email.toLowerCase() === currentUserEmail.toLowerCase())
    : null;
  const workspaceIsCloudImported = syncRoot?.origin === 'CloudImported' ||
    syncRoot?.origin === 2;
  const effectiveWorkspaceRole = workspaceIsCloudImported
    ? syncRoot?.remoteRole
    : currentWorkspaceMember?.role;
  const currentUserOwnsWorkspace = effectiveWorkspaceRole
    ? isOwnerRole(effectiveWorkspaceRole)
    : !currentUserEmail;
  const workspaceIsTaskShareOnly =
    isTaskShareWorkspace(workspace ?? { accessKind: 'Membership' }) ||
    syncRoot?.remoteAccessKind === 'TaskShare';
  const currentUserHasReadOnlyWorkspaceAccess =
    Boolean(effectiveWorkspaceRole && isReadOnlyRole(effectiveWorkspaceRole));
  const hasWorkspace = Boolean(workspace?.id);
  const workspaceIsSystemAllTasks = workspace ? isSystemAllTasksWorkspace(workspace) : false;
  const creatableWorkspaces = useMemo(
    () => workspaces.filter((candidate) =>
      !isSystemAllTasksWorkspace(candidate) &&
      !isTaskShareWorkspace(candidate) &&
      (!candidate.role || !isReadOnlyRole(candidate.role))),
    [workspaces],
  );
  const canManageSharing = !workspaceIsCloudImported &&
    currentUserOwnsWorkspace &&
    !workspaceIsTaskShareOnly &&
    !workspaceIsSystemAllTasks;
  const canManageWorkspaceMetadata = currentUserOwnsWorkspace &&
    !workspaceIsTaskShareOnly &&
    !workspaceIsSystemAllTasks;
  const archiveViewIsActive = currentView?.filter.archive === 'Archived';
  const canCreateTask = hasWorkspace &&
    !archiveViewIsActive &&
    !workspaceIsTaskShareOnly &&
    !currentUserHasReadOnlyWorkspaceAccess;
  const canUseBatchActions = hasWorkspace &&
    !workspaceIsTaskShareOnly &&
    !currentUserHasReadOnlyWorkspaceAccess;
  const [filters, setFilters] = useState<TaskWallFilters>(emptyTaskWallFilters);
  const [pendingDeletedNoteIds, setPendingDeletedNoteIds] = useState<string[]>([]);
  const [editModeIsEnabled, setEditModeIsEnabled] = useState(false);
  const [selectedTaskIds, setSelectedTaskIds] = useState<string[]>([]);
  const [batchReopenIsOpen, setBatchReopenIsOpen] = useState(false);
  const [batchPermanentDeleteIsOpen, setBatchPermanentDeleteIsOpen] = useState(false);
  const [batchShareIsOpen, setBatchShareIsOpen] = useState(false);
  const [cloudSyncIsOpen, setCloudSyncIsOpen] = useState(false);
  const [cloudSyncWorkspaceId, setCloudSyncWorkspaceId] = useState<string | null>(null);
  const [wallSort, setWallSort] = useState<SavedViewSort>(() => ({
    field: currentView?.sort.field ?? 'lastTouchedAt',
    direction: currentView?.sort.direction ?? 'desc',
  }));
  const longPressTimerRef = useRef<number | null>(null);
  const longPressHandledRef = useRef(false);
  const visibleTaskItems = useMemo(
    () => sortTaskItems(
      applyTaskWallFilters(taskItems, filters, currentUserEmail, projects),
      wallSort,
    ),
    [currentUserEmail, filters, projects, taskItems, wallSort],
  );
  const [draftTaskIsOpen, setDraftTaskIsOpen] = useState(false);
  const focusedTaskItem = selectedTaskId
    ? selectedTask?.parentTaskItemId
      ? visibleTaskItems.find((taskItem) => taskItem.id === selectedTask.parentTaskItemId) ?? selectedTask
      : visibleTaskItems.find((taskItem) => taskItem.id === selectedTaskId) ??
        (selectedTask?.id === selectedTaskId ? selectedTask : null)
    : null;
  const focusModeIsEnabled = Boolean(selectedTaskId) || draftTaskIsOpen;
  const displayedTaskItems = selectedTaskId || draftTaskIsOpen
    ? focusedTaskItem ? [focusedTaskItem] : []
    : visibleTaskItems;
  const focusedTaskWorkspace = selectedTask
    ? workspaces.find((candidate) => candidate.id === selectedTask.workspaceId) ?? null
    : null;
  const canCreateSubtasks = Boolean(
    focusedTaskWorkspace &&
    !isTaskShareWorkspace(focusedTaskWorkspace) &&
    (!focusedTaskWorkspace.role || !isReadOnlyRole(focusedTaskWorkspace.role)),
  );
  const projectByName = useMemo(
    () => new Map(projects.map((project) => [project.name.toLowerCase(), project])),
    [projects],
  );
  const filterOptions = useMemo(
    () => buildTaskFilterOptions(taskItems, colorOptions),
    [colorOptions, taskItems],
  );
  useEffect(() => {
    setFilters((currentFilters) => {
      if (!currentFilters.sharedWith) {
        return currentFilters;
      }

      const selectedEmail = currentFilters.sharedWith.trim().toLowerCase();
      return filterOptions.sharedWith.some((email) => email.toLowerCase() === selectedEmail)
        ? currentFilters
        : { ...currentFilters, sharedWith: '' };
    });
  }, [filterOptions.sharedWith]);
  const filtersAreActive = taskWallFiltersAreActive(filters);
  const selectedProjectIds = filters.projectIds;
  const [draftTaskTarget, setDraftTaskTarget] = useState<DraftTaskTarget | null>(null);
  const toggleProjectFilter = useCallback((projectId: string) => {
    setFilters((currentFilters) => {
      if (!projectId) {
        return {
          ...currentFilters,
          category: '',
          projectIds: [],
        };
      }

      const nextProjectIds = currentFilters.projectIds.includes(projectId)
        ? currentFilters.projectIds.filter((currentProjectId) => currentProjectId !== projectId)
        : [...currentFilters.projectIds, projectId];

      return {
        ...currentFilters,
        category: '',
        projectIds: nextProjectIds,
      };
    });
  }, []);
  useEffect(() => {
    setPendingDeletedNoteIds([]);
  }, [selectedTaskId]);

  useEffect(() => {
    setWallSort({
      field: currentView?.sort.field ?? 'lastTouchedAt',
      direction: currentView?.sort.direction ?? 'desc',
    });
  }, [currentView?.id, currentView?.sort.direction, currentView?.sort.field]);

  useEffect(() => {
    setSelectedTaskIds((currentIds) =>
      currentIds.filter((id) => visibleTaskItems.some((taskItem) => taskItem.id === id)),
    );
  }, [visibleTaskItems]);

  const toggleSelectedTask = useCallback((taskItemId: string) => {
    setSelectedTaskIds((currentIds) =>
      currentIds.includes(taskItemId)
        ? currentIds.filter((currentId) => currentId !== taskItemId)
        : [...currentIds, taskItemId],
    );
  }, []);

  const closeEditMode = useCallback(() => {
    setEditModeIsEnabled(false);
    setSelectedTaskIds([]);
    setBatchReopenIsOpen(false);
    setBatchPermanentDeleteIsOpen(false);
    setBatchShareIsOpen(false);
  }, []);

  const clearLongPressTimer = useCallback(() => {
    if (longPressTimerRef.current) {
      window.clearTimeout(longPressTimerRef.current);
      longPressTimerRef.current = null;
    }
  }, []);

  const startTaskLongPress = useCallback((
    event: ReactPointerEvent<HTMLElement>,
    taskItemId: string,
  ) => {
    if (
      event.button !== 0 ||
      editModeIsEnabled ||
      focusModeIsEnabled ||
      isTextEditingTarget(event.target)
    ) {
      return;
    }

    clearLongPressTimer();
    longPressHandledRef.current = false;
    longPressTimerRef.current = window.setTimeout(() => {
      setEditModeIsEnabled(true);
      setSelectedTaskIds((currentIds) =>
        currentIds.includes(taskItemId) ? currentIds : [...currentIds, taskItemId],
      );
      longPressHandledRef.current = true;
      longPressTimerRef.current = null;
    }, 420);
  }, [clearLongPressTimer, editModeIsEnabled, focusModeIsEnabled]);

  useEffect(() => () => clearLongPressTimer(), [clearLongPressTimer]);

  const closeFocusedTask = useCallback(async (destination: 'default' | 'wall' = 'default') => {
    if (!onBeforeTaskNavigation()) {
      return;
    }

    const idsToDelete = pendingDeletedNoteIds;

    setPendingDeletedNoteIds([]);

    for (const entryId of idsToDelete) {
      await onDeleteTimelineEntry(entryId);
    }

    onCloseTaskItem(destination);
  }, [onBeforeTaskNavigation, onCloseTaskItem, onDeleteTimelineEntry, pendingDeletedNoteIds]);

  const openParentTask = useCallback(async () => {
    if (!selectedTask?.parentTaskItemId || !onBeforeTaskNavigation()) {
      return;
    }

    const idsToDelete = pendingDeletedNoteIds;
    setPendingDeletedNoteIds([]);
    for (const entryId of idsToDelete) {
      await onDeleteTimelineEntry(entryId);
    }

    onCloseTaskItem('parent');
  }, [
    onBeforeTaskNavigation,
    onDeleteTimelineEntry,
    onCloseTaskItem,
    pendingDeletedNoteIds,
    selectedTask,
  ]);

  const openCreateTask = useCallback(() => {
    if (workspaceIsSystemAllTasks) {
      if (creatableWorkspaces.length === 0) {
        onShowToast(t('createBoardBeforeTasks'), 'error');
        window.dispatchEvent(new CustomEvent('dumptether:open-board-create'));
        return;
      }

      if (focusedTaskItem || draftTaskIsOpen) {
        return;
      }

      setDraftTaskTarget({
        workspaceId: '',
        workspaceName: t('chooseBoard'),
        workspaceColor: null,
        projects: [],
        selectedProjectId: '',
      });
      setDraftTaskIsOpen(true);
      return;
    }

    const targetWorkspace = workspace;

    if (!targetWorkspace?.id) {
      onShowToast(t('createBoardBeforeTasks'), 'error');
      return;
    }

    if (!canCreateTask) {
      onShowToast(t('boardAccessCannotCreateTasks'), 'error');
      return;
    }

    if (focusedTaskItem || draftTaskIsOpen) {
      return;
    }

    setDraftTaskTarget({
      workspaceId: targetWorkspace.id,
      workspaceName: targetWorkspace.name,
      workspaceColor: targetWorkspace.color,
      projects,
      selectedProjectId: selectedProjectIds[0] ?? '',
    });
    setDraftTaskIsOpen(true);
  }, [
    canCreateTask,
    creatableWorkspaces,
    draftTaskIsOpen,
    focusedTaskItem,
    onShowToast,
    projects,
    selectedProjectIds,
    t,
    workspace,
    workspaceIsSystemAllTasks,
  ]);

  const openCloudSync = useCallback((workspaceId: string | null | undefined) => {
    if (!workspaceId) {
      onShowToast(t('noBoardSelected'), 'error');
      return;
    }

    setCloudSyncWorkspaceId(workspaceId);
    setCloudSyncIsOpen(true);
  }, [onShowToast, t]);

  useEffect(() => {
    if (!selectedTaskId) {
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
  }, [closeFocusedTask, selectedTaskId]);

  useEffect(() => {
    const handleKeyDown = (event: globalThis.KeyboardEvent) => {
      if (isTextEditingTarget(event.target)) {
        return;
      }

      if (event.altKey && event.key.toLowerCase() === 'n') {
        event.preventDefault();
        openCreateTask();
      }

      if (event.altKey && event.key.toLowerCase() === 'x') {
        event.preventDefault();
        if (canUseBatchActions && visibleTaskItems.length > 0) {
          if (editModeIsEnabled) {
            closeEditMode();
          } else {
            setEditModeIsEnabled(true);
          }
        }
      }
    };

    window.addEventListener('keydown', handleKeyDown);

    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [canUseBatchActions, closeEditMode, editModeIsEnabled, openCreateTask, visibleTaskItems.length]);

  return (
    <section
      className="task-board"
      aria-labelledby="task-board-title"
      data-focus-mode={focusModeIsEnabled}
      data-loading={isLoading && !focusModeIsEnabled}
      data-refreshing={isRefreshing && !focusModeIsEnabled}
    >
      {!focusModeIsEnabled ? (
        <WorkspaceHeader
          sort={wallSort}
          onChangeSort={setWallSort}
          onCreateProject={onCreateProject}
          onDeleteProject={onDeleteProject}
          onSelectProjectFilter={toggleProjectFilter}
          onUpdateProject={onUpdateProject}
          onUpdateWorkspace={onUpdateWorkspace}
          onCreateWorkspaceInvitation={onCreateWorkspaceInvitation}
          onRemoveWorkspaceMember={onRemoveWorkspaceMember}
          onRevokeWorkspaceInvitation={onRevokeWorkspaceInvitation}
          onUpdateWorkspaceMemberRole={onUpdateWorkspaceMemberRole}
          colorOptions={colorOptions}
          invitations={workspaceInvitations}
          members={workspaceMembers}
          projects={projects}
          selectedProjectIds={selectedProjectIds}
          t={t}
          workspace={workspace}
          canManageWorkspaceMetadata={canManageWorkspaceMetadata}
          canManageSharing={canManageSharing}
          canSyncWorkspace={
            localDesktopSessionIsActive &&
            hasWorkspace &&
            !workspaceIsSystemAllTasks &&
            currentUserOwnsWorkspace
          }
          onOpenCloudSync={() => openCloudSync(workspace?.id)}
          syncRoot={syncRoot}
        />
      ) : null}

      {!focusModeIsEnabled ? (
        <TaskFilterBar
          filters={filters}
          filtersAreActive={filtersAreActive}
          onChange={setFilters}
          onReset={() => setFilters(emptyTaskWallFilters)}
          options={{
            ...filterOptions,
            statuses: uniqueSorted([...filterOptions.statuses, ...statusOptions]),
          }}
          t={t}
        />
      ) : null}

      <div className="task-grid" aria-busy={isLoading}>
        {!isLoading && displayedTaskItems.length === 0 ? (
          <p className="empty-copy board-empty">
            {!hasWorkspace
              ? t('noBoardSelected')
              : filtersAreActive
              ? t('noTasksMatch')
              : t('noTasks')}
          </p>
        ) : null}

        {draftTaskIsOpen ? (
          <DraftTaskCard
            onCancel={() => {
              setDraftTaskIsOpen(false);
              setDraftTaskTarget(null);
            }}
            onCreateTaskItem={onCreateTaskItem}
            onCreated={(createdTask) => {
              setDraftTaskIsOpen(false);
              setDraftTaskTarget(null);
              onSelectTaskItem(createdTask.id, createdTask.workspaceId, 'wall');
            }}
            onWorkspaceChange={workspaceIsSystemAllTasks
              ? (workspaceId) => {
                  const targetWorkspace = creatableWorkspaces.find((candidate) =>
                    candidate.id === workspaceId) ?? null;
                  setDraftTaskTarget({
                    workspaceId: targetWorkspace?.id ?? '',
                    workspaceName: targetWorkspace?.name ?? '',
                    workspaceColor: targetWorkspace?.color ?? null,
                    projects: [],
                    selectedProjectId: '',
                  });
                }
              : undefined}
            projects={draftTaskTarget?.projects ?? projects}
            selectedProjectId={draftTaskTarget?.selectedProjectId ?? selectedProjectIds[0] ?? ''}
            t={t}
            templates={templates}
            workspaceColor={draftTaskTarget?.workspaceColor ?? workspace?.color ?? null}
            workspaceId={draftTaskTarget?.workspaceId ?? workspace?.id ?? ''}
            workspaceName={draftTaskTarget?.workspaceName ?? workspace?.name ?? t('board')}
            workspaceOptions={workspaceIsSystemAllTasks ? creatableWorkspaces : []}
          />
        ) : null}

        {selectedTaskId && !focusedTaskItem ? (
          <div className="task-card task-card-pending-detail">
            <div className="task-card-detail" aria-busy={isLoadingDetail}>
              <div className="task-detail-loading-indicator">
                <BoardLoadingState compact t={t} />
              </div>
            </div>
          </div>
        ) : null}

        {displayedTaskItems.map((taskItem) => {
          const isExpanded = selectedTaskId === taskItem.id ||
            selectedTask?.parentTaskItemId === taskItem.id;
          const isSelectedForEdit = selectedTaskIds.includes(taskItem.id);
          const taskCategoryNames = splitTaskCategories(taskItem.category);
          const sourceWorkspace = workspaceIsSystemAllTasks
            ? workspaces.find((candidate) => candidate.id === taskItem.workspaceId) ?? null
            : null;

          return (
            <article
              className="task-card"
              data-shows-workspace-source={Boolean(sourceWorkspace)}
              data-expanded={isExpanded}
              data-edit-selected={isSelectedForEdit}
              data-edit-mode={editModeIsEnabled}
              data-task-selection-target="true"
              data-state={getTaskState(taskItem)}
              key={taskItem.id}
              style={{
                ...getTaskCardStyle(taskItem.color),
                '--task-workspace-color': sourceWorkspace?.color ?? '#184c48',
              } as CSSProperties}
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
              <div
                aria-expanded={isExpanded}
                aria-pressed={editModeIsEnabled ? isSelectedForEdit : undefined}
                className="task-card-button"
                onClick={() => {
                  if (longPressHandledRef.current) {
                    longPressHandledRef.current = false;
                    return;
                  }

                  if (editModeIsEnabled) {
                    toggleSelectedTask(taskItem.id);
                    return;
                  }

                  if (isExpanded) {
                    void closeFocusedTask();
                  } else {
                    onSelectTaskItem(taskItem.id, taskItem.workspaceId, 'wall');
                  }
                }}
                onPointerCancel={clearLongPressTimer}
                onPointerDown={(event) => startTaskLongPress(event, taskItem.id)}
                onPointerLeave={clearLongPressTimer}
                onPointerUp={clearLongPressTimer}
                onKeyDown={(event) => {
                  if (event.key !== 'Enter' && event.key !== ' ') {
                    return;
                  }

                  event.preventDefault();
                  if (editModeIsEnabled) {
                    toggleSelectedTask(taskItem.id);
                  } else if (isExpanded) {
                    void closeFocusedTask();
                  } else {
                    onSelectTaskItem(taskItem.id, taskItem.workspaceId, 'wall');
                  }
                }}
                role="button"
                tabIndex={0}
                title={isExpanded ? t('backToWall') : taskItem.title}
              >
                {sourceWorkspace ? (
                  <span
                    className="task-card-workspace-source"
                    title={`${t('board')}: ${formatWorkspaceName(sourceWorkspace.name, t)}`}
                  >
                    <span aria-hidden="true" className="task-card-workspace-source-dot" />
                    {formatWorkspaceName(sourceWorkspace.name, t)}
                  </span>
                ) : null}
                <span className="task-card-topline">
                  <span className="task-card-title">{taskItem.title}</span>
                  {taskItem.noteCount > 0 ? (
                    <span className="note-count">{taskItem.noteCount}</span>
                  ) : null}
                  {taskItem.subtaskCount > 0 ? (
                    <span className="subtask-stack-count" title={`${taskItem.subtaskCount} ${t('subtaskCount')}`}>
                      {taskItem.subtaskCount}
                    </span>
                  ) : null}
                  {taskItem.shares.length > 0 ? (
                    <span className="note-count share-count" title={t('sharing')}>
                      <Icon name="user" />
                      {taskItem.shares.length}
                    </span>
                  ) : null}
                  <TaskSyncIndicator syncState={taskItem.syncState} t={t} />
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
                {taskItem.subtaskCount > 0 ? (
                  <span className="task-card-subtask-notes" aria-label={`${taskItem.subtaskCount} ${t('subtaskCount')}`}>
                    <span className="task-card-subtask-heading">{t('subtasks')}</span>
                    {(taskItem.subtaskPreviews ?? []).map((subtask) => (
                      <span
                        className="task-card-subtask-note"
                        key={subtask.id}
                        onClick={(event) => {
                          event.stopPropagation();
                          onSelectTaskItem(subtask.id, taskItem.workspaceId, 'wall');
                        }}
                        onKeyDown={(event) => {
                          if (event.key !== 'Enter' && event.key !== ' ') return;
                          event.preventDefault();
                          event.stopPropagation();
                          onSelectTaskItem(subtask.id, taskItem.workspaceId, 'wall');
                        }}
                        onPointerDown={(event) => event.stopPropagation()}
                        role="button"
                        style={getTaskCardStyle(subtask.color)}
                        tabIndex={0}
                        title={subtask.status ? `${subtask.title} · ${subtask.status}` : subtask.title}
                      >
                        {subtask.title}
                      </span>
                    ))}
                    {taskItem.subtaskCount > (taskItem.subtaskPreviews?.length ?? 0) ? (
                      <span className="task-card-subtask-overflow">
                        … +{taskItem.subtaskCount - (taskItem.subtaskPreviews?.length ?? 0)}
                      </span>
                    ) : null}
                  </span>
                ) : null}
                <span className="task-card-meta">
                  {taskItem.status ? (
                    <TaskMetaChip
                      icon="status"
                      label={t('status')}
                      style={getContextChipStyle(statusColors[taskItem.status] ?? null)}
                      value={taskItem.status}
                    />
                  ) : null}
                  <span title={`${t('lastUpdated')}: ${formatRelativeDate(taskItem.lastTouchedAt)}`}>
                    <Icon name="clock" />
                    {formatRelativeDate(taskItem.lastTouchedAt)}
                  </span>
                  {taskItem.followUpAt ? (
                    <span
                      className="follow-up-chip"
                      data-tone={getFollowUpTone(taskItem.followUpAt)}
                      title={`${t('followUpDate')}: ${formatFullDate(taskItem.followUpAt)}`}
                    >
                      <Icon name="calendarX" />
                      {t('followUp')} {formatFullDate(taskItem.followUpAt)}
                    </span>
                  ) : null}
                </span>
                <TaskBadges taskItem={taskItem} t={t} />
                <span className="task-card-created">
                  {taskCategoryNames.length > 0 && selectedProjectIds.length === 0 ? (
                    <span
                      className="task-card-category-markers"
                      title={`${t('category')}: ${taskCategoryNames.join(', ')}`}
                    >
                      <Icon name="tag" />
                      {taskCategoryNames.map((categoryName) => {
                        const categoryProject = projectByName.get(categoryName.toLowerCase()) ?? null;

                        return (
                          <span
                            aria-hidden="true"
                            className="task-card-category-dot"
                            key={categoryName}
                            style={getContextChipStyle(categoryProject?.color ?? null)}
                          />
                        );
                      })}
                    </span>
                  ) : null}
                  <span title={`${t('created')}: ${formatFullDate(taskItem.createdAt)}`}>
                    {formatFullDate(taskItem.createdAt)}
                  </span>
                </span>
              </div>

              {isExpanded ? (
                <div className="task-card-detail" aria-busy={isLoadingDetail}>
                  {!selectedTask || (
                    selectedTask.id !== taskItem.id &&
                    selectedTask.parentTaskItemId !== taskItem.id
                  ) ? (
                    <div className="task-detail-loading-indicator">
                      <BoardLoadingState compact t={t} />
                    </div>
                  ) : (
                    <TaskDetail
                      onAddTimelineEntry={onAddTimelineEntry}
                      onCreateSubtask={(requestBody) => onCreateSubtask(selectedTask, requestBody)}
                      onListSubtasks={() => onListSubtasks(selectedTask)}
                      onOpenSubtask={(subtask) =>
                        onSelectTaskItem(subtask.id, subtask.workspaceId, 'parent')}
                      onArchive={onArchive}
                      onDeleteSubtask={() => onDeleteSubtask(
                        taskItems.find((candidate) => candidate.id === selectedTask.parentTaskItemId) ?? selectedTask,
                        selectedTask.id,
                      )}
                      onClose={closeFocusedTask}
                      onOpenBoard={() => closeFocusedTask('wall')}
                      onOpenParent={openParentTask}
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
                      onCreateTaskShareLink={onCreateTaskShareLink}
                      onRevokeTaskShare={onRevokeTaskShare}
                      onUpdateTaskShareRole={onUpdateTaskShareRole}
                      onUpdateTaskItem={onUpdateTaskItem}
                      onUpdateTimelineEntry={onUpdateTimelineEntry}
                      onUnsavedChangesChange={onUnsavedChangesChange}
                      onImportTemplate={() => onImportTaskTemplate(selectedTask.id)}
                      onRequestSync={() => openCloudSync(selectedTask.workspaceId)}
                      colorOptions={colorOptions}
                      canCreateSubtasks={canCreateSubtasks}
                      canManageSharing={canManageSharing && !selectedTask.parentTaskItemId}
                      canDeleteSubtask={Boolean(
                        focusedTaskWorkspace &&
                        (!focusedTaskWorkspace.role || isOwnerRole(focusedTaskWorkspace.role)) &&
                        !workspaceIsCloudImported,
                      )}
                      templateCanBeImported={Boolean(
                        selectedTask.taskTemplateId &&
                        selectedTask.template &&
                        !templates.some((template) => template.id === selectedTask.taskTemplateId) &&
                        !importedTemplateSourceIds.includes(selectedTask.taskTemplateId),
                      )}
                      pendingDeletedNoteIds={pendingDeletedNoteIds}
                      projects={projects}
                      statusOptions={statusOptions}
                      statusColors={statusColors}
                      workspaceName={focusedTaskWorkspace?.name ?? workspace?.name ?? t('board')}
                      parentTaskTitle={taskItems.find((candidate) => candidate.id === selectedTask.parentTaskItemId)?.title ?? null}
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

      {(isLoading || isRefreshing) && !focusModeIsEnabled ? (
        <div className="board-loading-indicator">
          <BoardLoadingState compact t={t} />
        </div>
      ) : null}
      {!isLoading &&
      (canCreateTask || (canUseBatchActions && visibleTaskItems.length > 0)) &&
      !focusModeIsEnabled ? (
        <FloatingBoardActions
          archiveModeIsActive={archiveViewIsActive}
          canCreateTask={canCreateTask}
          editModeIsEnabled={editModeIsEnabled}
          taskCount={visibleTaskItems.length}
          colorOptions={colorOptions}
          onBatchUpdate={async (requestBody) => {
            await onUpdateTaskItems(selectedTaskIds, requestBody);
            setSelectedTaskIds([]);
          }}
          onCopyTaskItemsToWorkspace={async (workspaceId) => {
            await onCopyTaskItemsToWorkspace(selectedTaskIds, workspaceId);
            setSelectedTaskIds([]);
            closeEditMode();
          }}
          onOpenCreateTask={openCreateTask}
          onOpenBatchArchive={() => {
            void (async () => {
              await onArchiveTaskItems(selectedTaskIds);
              closeEditMode();
            })();
          }}
          onOpenBatchReopen={() => setBatchReopenIsOpen(true)}
          onOpenBatchPermanentDelete={() => setBatchPermanentDeleteIsOpen(true)}
          onOpenBatchShare={() => setBatchShareIsOpen(true)}
          onToggleEditMode={() =>
            editModeIsEnabled ? closeEditMode() : setEditModeIsEnabled(true)}
          selectedTaskCount={selectedTaskIds.length}
          canManageSharing={canManageSharing}
          canPermanentlyDelete={selectedTaskIds.length === 0
            ? workspaceIsSystemAllTasks
              ? workspaces.some((candidate) =>
                  !isSystemAllTasksWorkspace(candidate) &&
                  !isTaskShareWorkspace(candidate) &&
                  (!candidate.role || isOwnerRole(candidate.role)))
              : currentUserOwnsWorkspace && !workspaceIsTaskShareOnly
            : selectedTaskIds.every((taskItemId) => {
                const selectedItem = taskItems.find((taskItem) => taskItem.id === taskItemId);
                const selectedItemWorkspace = selectedItem
                  ? workspaces.find((candidate) => candidate.id === selectedItem.workspaceId)
                  : null;
                return Boolean(
                  selectedItemWorkspace &&
                  !isTaskShareWorkspace(selectedItemWorkspace) &&
                  (!selectedItemWorkspace.role || isOwnerRole(selectedItemWorkspace.role)),
                );
              })}
          projects={projects}
          statusOptions={statusOptions}
          t={t}
          workspaces={workspaces}
        />
      ) : null}
      {batchShareIsOpen ? (
        <ShareDialog
          existingTaskShares={[]}
          onClose={() => setBatchShareIsOpen(false)}
          onCreate={async (email, role) =>
            await onCreateTaskShareLinks({
              email,
              taskItemIds: selectedTaskIds,
              role: role as TaskItemShareRole,
            })}
          onRevokeTaskShare={undefined}
          pendingInvitations={[]}
          roleMode="task"
          t={t}
          title={`${selectedTaskIds.length} ${t('selectedTasks')}`}
        />
      ) : null}
      {batchReopenIsOpen ? (
        <ReopenDialog
          onClose={() => setBatchReopenIsOpen(false)}
          onReopen={async (note) => {
            await onReopenTaskItems(selectedTaskIds, note);
            closeEditMode();
          }}
          t={t}
          taskTitle={`${selectedTaskIds.length} ${t('selectedTasks')}`}
        />
      ) : null}
      {batchPermanentDeleteIsOpen ? (
        <PermanentDeleteDialog
          count={selectedTaskIds.length}
          onClose={() => setBatchPermanentDeleteIsOpen(false)}
          onDelete={async () => {
            await onDeleteTaskItemsPermanently(selectedTaskIds);
            closeEditMode();
          }}
          t={t}
        />
      ) : null}
      {cloudSyncIsOpen && cloudSyncWorkspaceId ? (
        <CloudSyncDialog
          cloudAccount={cloudSyncAccount}
          onClose={() => {
            setCloudSyncIsOpen(false);
            setCloudSyncWorkspaceId(null);
          }}
          onSync={(requestBody) => onSyncWorkspaceWithCloud(cloudSyncWorkspaceId, requestBody)}
          taskItems={taskItems}
          t={t}
          workspaceName={
            workspaces.find((candidate) => candidate.id === cloudSyncWorkspaceId)?.name ??
            workspace?.name ??
            t('board')
          }
        />
      ) : null}
    </section>
  );
}

function sortTaskItems(
  taskItems: TaskItemSummaryResponse[],
  sort: SavedViewSort,
) {
  const field = sort.field ?? 'lastTouchedAt';
  const direction = sort.direction === 'asc' ? 1 : -1;

  return [...taskItems].sort((left, right) => {
    const leftValue = getTaskSortValue(left, field);
    const rightValue = getTaskSortValue(right, field);

    if (leftValue === null || rightValue === null) {
      if (leftValue === rightValue) {
        return left.title.localeCompare(right.title, undefined, { sensitivity: 'base' });
      }

      return leftValue === null ? 1 : -1;
    }

    const comparison = leftValue.localeCompare(rightValue, undefined, {
      numeric: true,
      sensitivity: 'base',
    });

    return comparison === 0
      ? left.title.localeCompare(right.title, undefined, { sensitivity: 'base' })
      : comparison * direction;
  });
}

function getTaskSortValue(
  taskItem: TaskItemSummaryResponse,
  field: NonNullable<SavedViewSort['field']>,
) {
  switch (field) {
    case 'createdAt':
      return taskItem.createdAt;
    case 'followUpAt':
      return taskItem.followUpAt;
    case 'title':
      return taskItem.title;
    case 'status':
      return taskItem.status ?? '';
    case 'lastTouchedAt':
    default:
      return taskItem.lastTouchedAt;
  }
}
