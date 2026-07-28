import {
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
  ArchiveResolutionResponse,
  ArchiveTaskItemRequest,
  CloudSyncAccountResponse,
  CreateTaskShareLinkRequest,
  CreateTaskShareRequest,
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
import {
  ArchiveDialog,
  PermanentDeleteDialog,
  ReopenDialog,
} from '../task-detail/TaskDialogs';
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
  archiveDialogIsOpen,
  archiveResolutions,
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
  onCloseArchiveDialog,
  onCopyTaskItemsToWorkspace,
  onCreateProject,
  onCreateTaskShareLink,
  onCreateTaskShareLinks,
  onCreateTaskItem,
  onCreateWorkspaceInvitation,
  onDeleteProject,
  onDeleteTimelineEntry,
  onImportTaskTemplate,
  onOpenArchiveDialog,
  onReopen,
  onReopenTaskItems,
  onDeleteTaskItemsPermanently,
  onRemoveWorkspaceMember,
  onRevokeTaskShare,
  onRevokeWorkspaceInvitation,
  onCloseTaskItem,
  onSelectTaskItem,
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
  archiveDialogIsOpen: boolean;
  archiveResolutions: ArchiveResolutionResponse[];
  colorOptions: string[];
  currentView: SavedViewResponse | null;
  currentUserEmail: string | null;
  cloudSyncAccount: CloudSyncAccountResponse | null;
  isLoading: boolean;
  isLoadingDetail: boolean;
  isRefreshing: boolean;
  localDesktopSessionIsActive: boolean;
  onAddTimelineEntry: (note: string, fieldValues?: FieldValueMap) => Promise<void>;
  onArchive: (requestBody: ArchiveTaskItemRequest) => Promise<void>;
  onArchiveTaskItems: (taskItemIds: string[], requestBody: ArchiveTaskItemRequest) => Promise<void>;
  onCloseArchiveDialog: () => void;
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
  onCreateWorkspaceInvitation: (
    requestBody: CreateWorkspaceInvitationRequest,
  ) => Promise<WorkspaceInvitationResponse>;
  onDeleteProject: (projectId: string) => Promise<void>;
  onDeleteTimelineEntry: (entryId: string) => Promise<void>;
  onImportTaskTemplate: (taskItemId: string) => Promise<void>;
  onOpenArchiveDialog: () => void;
  onReopen: (note?: string) => Promise<void>;
  onReopenTaskItems: (taskItemIds: string[], note?: string) => Promise<void>;
  onDeleteTaskItemsPermanently: (taskItemIds: string[]) => Promise<void>;
  onRemoveWorkspaceMember: (userId: string) => Promise<void>;
  onRevokeTaskShare: (taskItemId: string, shareId: string) => Promise<void>;
  onRevokeWorkspaceInvitation: (id: string) => Promise<void>;
  onCloseTaskItem: () => void;
  onSelectTaskItem: (id: string, workspaceId: string) => void;
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
  const currentUserOwnsWorkspace = currentWorkspaceMember
    ? isOwnerRole(currentWorkspaceMember.role)
    : !currentUserEmail;
  const workspaceIsTaskShareOnly = isTaskShareWorkspace(workspace ?? { accessKind: 'Membership' });
  const currentUserHasReadOnlyWorkspaceAccess = currentWorkspaceMember
    ? isReadOnlyRole(currentWorkspaceMember.role)
    : false;
  const hasWorkspace = Boolean(workspace?.id);
  const workspaceIsSystemAllTasks = workspace ? isSystemAllTasksWorkspace(workspace) : false;
  const canManageSharing = currentUserOwnsWorkspace &&
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
  const [batchArchiveIsOpen, setBatchArchiveIsOpen] = useState(false);
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
    ? visibleTaskItems.find((taskItem) => taskItem.id === selectedTaskId) ?? null
    : null;
  const focusModeIsEnabled = Boolean(focusedTaskItem) || draftTaskIsOpen;
  const displayedTaskItems = focusedTaskItem || draftTaskIsOpen
    ? focusedTaskItem ? [focusedTaskItem] : []
    : visibleTaskItems;
  const projectByName = useMemo(
    () => new Map(projects.map((project) => [project.name.toLowerCase(), project])),
    [projects],
  );
  const filterOptions = useMemo(
    () => buildTaskFilterOptions(taskItems, colorOptions),
    [colorOptions, taskItems],
  );
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
    setBatchArchiveIsOpen(false);
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
    event: ReactPointerEvent<HTMLButtonElement>,
    taskItemId: string,
  ) => {
    if (event.pointerType === 'mouse' || editModeIsEnabled || focusModeIsEnabled) {
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

  const closeFocusedTask = useCallback(async () => {
    const idsToDelete = pendingDeletedNoteIds;

    setPendingDeletedNoteIds([]);

    for (const entryId of idsToDelete) {
      await onDeleteTimelineEntry(entryId);
    }

    onCloseTaskItem();
  }, [onCloseTaskItem, onDeleteTimelineEntry, pendingDeletedNoteIds]);

  const openCreateTask = useCallback(() => {
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
    draftTaskIsOpen,
    focusedTaskItem,
    onShowToast,
    projects,
    selectedProjectIds,
    t,
    workspace,
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

  if (isLoading && !focusModeIsEnabled) {
    return (
      <section
        className="task-board"
        aria-busy="true"
        aria-labelledby="task-board-title"
        data-loading="true"
      >
        <BoardLoadingState t={t} />
      </section>
    );
  }

  return (
    <section
      className="task-board"
      aria-labelledby="task-board-title"
      data-focus-mode={focusModeIsEnabled}
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

      {isRefreshing && !focusModeIsEnabled ? (
        <div className="board-refresh-overlay">
          <BoardLoadingState compact t={t} />
        </div>
      ) : null}

      <div className="task-grid" aria-busy={isLoading}>
        {isLoading ? <p className="empty-copy">{t('loadingTasks')}</p> : null}
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
              onSelectTaskItem(createdTask.id, createdTask.workspaceId);
            }}
            projects={draftTaskTarget?.projects ?? projects}
            selectedProjectId={draftTaskTarget?.selectedProjectId ?? selectedProjectIds[0] ?? ''}
            t={t}
            templates={templates}
            workspaceColor={draftTaskTarget?.workspaceColor ?? workspace?.color ?? null}
            workspaceId={draftTaskTarget?.workspaceId ?? workspace?.id ?? ''}
            workspaceName={draftTaskTarget?.workspaceName ?? workspace?.name ?? t('board')}
          />
        ) : null}

        {displayedTaskItems.map((taskItem) => {
          const isExpanded = selectedTaskId === taskItem.id;
          const isSelectedForEdit = selectedTaskIds.includes(taskItem.id);
          const taskCategoryNames = splitTaskCategories(taskItem.category);

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
                    onSelectTaskItem(taskItem.id, taskItem.workspaceId);
                  }
                }}
                onPointerCancel={clearLongPressTimer}
                onPointerDown={(event) => startTaskLongPress(event, taskItem.id)}
                onPointerLeave={clearLongPressTimer}
                onPointerUp={clearLongPressTimer}
                title={isExpanded ? t('backToWall') : taskItem.title}
                type="button"
              >
                <span className="task-card-topline">
                  <span className="task-card-title">{taskItem.title}</span>
                  {taskItem.noteCount > 0 ? (
                    <span className="note-count">{taskItem.noteCount}</span>
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
                <span className="task-card-meta">
                  {taskItem.status ? (
                    <TaskMetaChip icon="status" label={t('status')} value={taskItem.status} />
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
                      onCreateTaskShareLink={onCreateTaskShareLink}
                      onRevokeTaskShare={onRevokeTaskShare}
                      onUpdateTaskShareRole={onUpdateTaskShareRole}
                      onUpdateTaskItem={onUpdateTaskItem}
                      onUpdateTimelineEntry={onUpdateTimelineEntry}
                      onImportTemplate={() => onImportTaskTemplate(selectedTask.id)}
                      onRequestSync={() => openCloudSync(selectedTask.workspaceId)}
                      colorOptions={colorOptions}
                      canManageSharing={canManageSharing}
                      templateCanBeImported={Boolean(
                        selectedTask.taskTemplateId &&
                        selectedTask.template &&
                        !templates.some((template) => template.id === selectedTask.taskTemplateId) &&
                        !importedTemplateSourceIds.includes(selectedTask.taskTemplateId),
                      )}
                      pendingDeletedNoteIds={pendingDeletedNoteIds}
                      projects={projects}
                      statusOptions={statusOptions}
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
          onOpenBatchArchive={() => setBatchArchiveIsOpen(true)}
          onOpenBatchReopen={() => setBatchReopenIsOpen(true)}
          onOpenBatchPermanentDelete={() => setBatchPermanentDeleteIsOpen(true)}
          onOpenBatchShare={() => setBatchShareIsOpen(true)}
          onToggleEditMode={() =>
            editModeIsEnabled ? closeEditMode() : setEditModeIsEnabled(true)}
          selectedTaskCount={selectedTaskIds.length}
          canManageSharing={canManageSharing}
          canPermanentlyDelete={currentUserOwnsWorkspace && !workspaceIsTaskShareOnly}
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
