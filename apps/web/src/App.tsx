import {
  type CSSProperties,
  type MouseEvent,
  type PointerEvent as ReactPointerEvent,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import { Icon } from './components/Icon';
import { TaskFilterBar } from './components/TaskFilterBar';
import { TaskBadges, TaskMetaChip } from './components/TaskMetadata';
import { ToastStack } from './components/ToastStack';
import {
  defaultAuthOptions,
  maxSidebarWidth,
  minSidebarWidth,
  sidebarWidthStorageKey,
  statusOptionsStorageKey,
  workspaceStorageKey,
  type ConnectionStatus,
  type ToastMessage,
  type ToastTone,
  type WorkspaceMode,
  languageStorageKey,
} from './appTypes';
import {
  clamp,
  findViewId,
  formatFullDate,
  formatRelativeDate,
  getErrorMessage,
  getInitialLanguage,
  getInitialMode,
  getInitialViewId,
  getInitialWorkspaceId,
  isAbortError,
  isOwnerRole,
  isReadOnlyRole,
  isSystemAllTasksWorkspace,
  isTaskShareWorkspace,
  isTextEditingTarget,
  pickSavedViewId,
  readStoredStringList,
  updateUrl,
} from './appUtils';
import {
  addTaskTimelineEntry,
  acceptShareLink,
  acceptIncomingWorkspaceInvitation,
  ApiError,
  archiveTaskItem,
  copyTaskItems,
  createArchiveResolution,
  createProject,
  createTaskShareLink,
  createTaskShareLinks,
  createTaskItem,
  createTaskTemplate,
  createWorkspace,
  createWorkspaceInvitation,
  deleteArchiveResolution,
  deleteProject,
  deleteTaskItemsPermanently,
  deleteTaskTimelineEntry,
  deleteTaskTemplate,
  deleteWorkspace,
  declineIncomingWorkspaceInvitation,
  developmentLogin,
  checkHealth,
  getAuthOptions,
  getCurrentUser,
  guestLogin,
  getTaskItem,
  getTaskTemplate,
  getWorkspace,
  leaveCurrentWorkspace,
  leaveTaskShare,
  leaveWorkspaceTaskShares,
  listArchiveResolutions,
  listIncomingTaskShares,
  listIncomingWorkspaceInvitations,
  listProjects,
  listSavedViews,
  listWorkspaceInvitations,
  listWorkspaceMembers,
  listTaskItems,
  listTaskTemplates,
  listTaskViewCounts,
  listWorkspaces,
  loginUser,
  logoutUser,
  reopenTaskItem,
  reopenTaskItems,
  registerUser,
  removeWorkspaceMember,
  revokeTaskShare,
  revokeWorkspaceInvitation,
  setCurrentWorkspaceId,
  isTemporarySession,
  updateArchiveResolution,
  updateProject,
  updateTaskShareRole,
  updateTaskItem,
  updateTaskTimelineEntry,
  updateTaskTemplate,
  updateWorkspace,
  updateWorkspaceById,
  updateWorkspaceMemberRole,
} from './api';
import './App.css';
import {
  ArchiveDialog,
  PermanentDeleteDialog,
  ReopenDialog,
} from './features/task-detail/TaskDialogs';
import { TaskDetail } from './features/task-detail/TaskDetail';
import { BoardLoadingState } from './features/task-wall/BoardLoadingState';
import { DraftTaskCard } from './features/task-wall/DraftTaskCard';
import { FloatingBoardActions } from './features/task-wall/FloatingBoardActions';
import { WorkspaceHeader } from './features/task-wall/WorkspaceHeader';
import { ShareDialog } from './features/sharing/ShareDialog';
import { Sidebar } from './features/navigation/Sidebar';
import {
  AccountPanel,
  AuthPanel,
  SettingsPanel,
} from './features/settings/AccountSettingsPanels';
import { TemplatesPage } from './features/templates/TemplatesPage';
import { startLiveUpdates, type LiveUpdateMessage } from './liveUpdates';
import { type Language, type Translate, translate } from './localization';
import {
  applyTaskWallFilters,
  buildTaskFilterOptions,
  emptyTaskWallFilters,
  getContextChipStyle,
  getFollowUpTone,
  getPrimaryProjectIdForCategories,
  getTaskCardStyle,
  getTaskColors,
  getTaskState,
  joinTaskCategories,
  mergeColorOptions,
  splitTaskCategories,
  taskWallFiltersAreActive,
  type TaskWallFilters,
  uniqueSorted,
} from './taskUtils';
import type {
  AuthClientOptionsResponse,
  ArchiveResolutionResponse,
  ArchiveTaskItemRequest,
  CurrentUserResponse,
  CreateArchiveResolutionRequest,
  CreateTaskShareRequest,
  CreateTaskShareLinkRequest,
  CreateTaskItemRequest,
  CreateWorkspaceInvitationRequest,
  FieldValueMap,
  LoginUserRequest,
  ProjectResponse,
  RegisterUserRequest,
  SavedViewResponse,
  TaskItemDetailResponse,
  TaskItemShareRole,
  TaskItemSummaryResponse,
  TaskShareInboxResponse,
  TaskShareLinkResponse,
  TaskTemplateDetailResponse,
  TaskTemplateLayoutResponse,
  WorkspaceInvitationInboxResponse,
  WorkspaceInvitationResponse,
  WorkspaceMemberResponse,
  UpdateArchiveResolutionRequest,
  UpdateProjectRequest,
  UpdateTaskShareRequest,
  UpdateTaskItemRequest,
  UpdateWorkspaceRequest,
  UpdateWorkspaceMemberRequest,
  UpsertFieldDefinitionRequest,
  WorkspaceResponse,
} from './types';
import {
  buildWorkspaceCacheKey,
  type CachedWorkspaceSnapshot,
  readCachedWorkspaceSnapshot,
  writeCachedWorkspaceSnapshot,
} from './workspaceCache';

function App() {
  const [savedViews, setSavedViews] = useState<SavedViewResponse[]>([]);
  const [workspaces, setWorkspaces] = useState<WorkspaceResponse[]>([]);
  const [workspace, setWorkspace] = useState<WorkspaceResponse | null>(null);
  const [projects, setProjects] = useState<ProjectResponse[]>([]);
  const [taskItems, setTaskItems] = useState<TaskItemSummaryResponse[]>([]);
  const [viewCounts, setViewCounts] = useState<Record<string, number>>({});
  const [archiveResolutions, setArchiveResolutions] = useState<ArchiveResolutionResponse[]>([]);
  const [workspaceMembers, setWorkspaceMembers] = useState<WorkspaceMemberResponse[]>([]);
  const [workspaceInvitations, setWorkspaceInvitations] = useState<WorkspaceInvitationResponse[]>([]);
  const [templates, setTemplates] = useState<TaskTemplateDetailResponse[]>([]);
  const [configuredStatuses, setConfiguredStatuses] = useState<string[]>(
    () => readStoredStringList(
      statusOptionsStorageKey,
      ['Active', 'Waiting', 'Follow-up', 'Blocked', 'Done'],
    ),
  );
  const [knownStatuses, setKnownStatuses] = useState<string[]>([]);
  const [mode, setMode] = useState<WorkspaceMode>(getInitialMode);
  const [currentViewId, setCurrentViewId] = useState<string | null>(getInitialViewId);
  const [selectedTaskId, setSelectedTaskId] = useState<string | null>(null);
  const [selectedTask, setSelectedTask] = useState<TaskItemDetailResponse | null>(null);
  const [archiveDialogIsOpen, setArchiveDialogIsOpen] = useState(false);
  const [sidebarIsCollapsed, setSidebarIsCollapsed] = useState(false);
  const [sidebarWidth, setSidebarWidth] = useState(() => {
    const storedWidth = Number(window.localStorage.getItem(sidebarWidthStorageKey));
    return Number.isFinite(storedWidth)
      ? clamp(storedWidth, minSidebarWidth, maxSidebarWidth)
      : 284;
  });
  const [settingsIsOpen, setSettingsIsOpen] = useState(false);
  const [accountIsOpen, setAccountIsOpen] = useState(false);
  const [authOptions, setAuthOptions] =
    useState<AuthClientOptionsResponse>(defaultAuthOptions);
  const [currentUser, setCurrentUser] = useState<CurrentUserResponse | null>(null);
  const [incomingWorkspaceInvitations, setIncomingWorkspaceInvitations] =
    useState<WorkspaceInvitationInboxResponse[]>([]);
  const [incomingTaskShares, setIncomingTaskShares] = useState<TaskShareInboxResponse[]>([]);
  const processedWorkspaceInviteTokenRef = useRef<string | null>(null);
  const [temporarySessionIsActive, setTemporarySessionIsActive] = useState(isTemporarySession);
  const [connectionStatus, setConnectionStatus] = useState<ConnectionStatus>('checking');
  const [lastPingedAt, setLastPingedAt] = useState<string | null>(null);
  const [toasts, setToasts] = useState<ToastMessage[]>([]);
  const [language, setLanguage] = useState<Language>(getInitialLanguage);
  const toastSequenceRef = useRef(0);
  const recentToastRef = useRef<{
    message: string;
    tone: ToastTone;
    shownAt: number;
  } | null>(null);
  const liveConnectionToastAtRef = useRef(0);
  const [selectedWorkspaceId, setSelectedWorkspaceId] = useState<string | null>(
    getInitialWorkspaceId,
  );
  const [taskColorOptions, setTaskColorOptions] = useState<string[]>([]);
  const [isLoadingWorkspace, setIsLoadingWorkspace] = useState(true);
  const [isRefreshingWorkspace, setIsRefreshingWorkspace] = useState(false);
  const [isLoadingDetail, setIsLoadingDetail] = useState(false);
  const [isLoadingAuth, setIsLoadingAuth] = useState(true);
  const [hasBootstrapped, setHasBootstrapped] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const workspaceLoadAbortRef = useRef<AbortController | null>(null);
  const workspaceLoadSequenceRef = useRef(0);
  const t = useCallback<Translate>((key) => translate(language, key), [language]);
  const statusOptions = useMemo(
    () => uniqueSorted([...configuredStatuses, ...knownStatuses]),
    [configuredStatuses, knownStatuses],
  );
  const accountNotificationCount = incomingWorkspaceInvitations.length + incomingTaskShares.length;

  const currentView = useMemo(
    () => savedViews.find((view) => view.id === currentViewId) ?? null,
    [currentViewId, savedViews],
  );

  const showToast = useCallback((message: string, tone: ToastMessage['tone'] = 'info') => {
    const now = Date.now();

    if (
      recentToastRef.current &&
      recentToastRef.current.message === message &&
      recentToastRef.current.tone === tone &&
      now - recentToastRef.current.shownAt < 1500
    ) {
      return;
    }

    recentToastRef.current = { message, tone, shownAt: now };
    toastSequenceRef.current += 1;
    const id = now + toastSequenceRef.current;
    const duration = tone === 'error' ? 12000 : 5200;
    setToasts((currentToasts) => [
      ...currentToasts.slice(-3),
      { id, message, tone },
    ]);
    window.setTimeout(() => {
      setToasts((currentToasts) => currentToasts.filter((toast) => toast.id !== id));
    }, duration);
  }, []);

  const dismissToast = useCallback((id: number) => {
    setToasts((currentToasts) => currentToasts.filter((toast) => toast.id !== id));
  }, []);

  useEffect(() => {
    if (errorMessage) {
      showToast(errorMessage, 'error');
    }
  }, [errorMessage, showToast]);

  const pingBackend = useCallback(async (showOfflineToast = false) => {
    const pingedAt = new Date().toISOString();

    try {
      await checkHealth();
      setConnectionStatus('online');
      setLastPingedAt(pingedAt);
    } catch {
      setConnectionStatus('offline');
      setLastPingedAt(pingedAt);
      if (showOfflineToast) {
        showToast(t('connectionLostToast'), 'error');
      }
    }
  }, [showToast, t]);

  const loadAuth = useCallback(async () => {
    setIsLoadingAuth(true);

    try {
      const options = await getAuthOptions();
      setAuthOptions(options);
      setConnectionStatus('online');

      try {
        const user = await getCurrentUser();
        const [workspaceInvites, taskShares] = await Promise.all([
          listIncomingWorkspaceInvitations().catch(() => []),
          listIncomingTaskShares().catch(() => []),
        ]);
        setCurrentUser(user);
        setIncomingWorkspaceInvitations(workspaceInvites);
        setIncomingTaskShares(taskShares);
        setTemporarySessionIsActive(isTemporarySession());
      } catch (error) {
        if (error instanceof ApiError && error.status === 401) {
          setCurrentUser(null);
          setIncomingWorkspaceInvitations([]);
          setIncomingTaskShares([]);
          setTemporarySessionIsActive(false);
        } else {
          throw error;
        }
      }
    } catch (error) {
      setConnectionStatus('offline');
      setErrorMessage(getErrorMessage(error));
    } finally {
      setIsLoadingAuth(false);
    }
  }, []);

  const applyWorkspaceSnapshot = useCallback((
    snapshot: CachedWorkspaceSnapshot,
    fallbackWorkspaceId: string | null,
  ) => {
    setWorkspaces(snapshot.workspaces);
    setWorkspace(snapshot.workspace);
    setSelectedWorkspaceId(snapshot.workspace?.id ?? fallbackWorkspaceId);
    setSavedViews(snapshot.savedViews);
    setProjects(snapshot.projects);
    setArchiveResolutions(snapshot.archiveResolutions);
    setWorkspaceMembers(snapshot.workspaceMembers ?? []);
    setWorkspaceInvitations(snapshot.workspaceInvitations ?? []);
    setTemplates(snapshot.templates);
    setTaskColorOptions(snapshot.taskColorOptions);
    setKnownStatuses(snapshot.knownStatuses);
    setCurrentViewId(snapshot.currentViewId);
    setTaskItems(snapshot.taskItems);
    setViewCounts(snapshot.viewCounts);
  }, []);

  const loadWorkspace = useCallback(
    async (
      preferredViewId: string | null = currentViewId,
      preferredWorkspaceId: string | null = selectedWorkspaceId,
      options: { force?: boolean; silent?: boolean } = {},
    ) => {
      const showLoading = !options.silent;
      const loadSequence = workspaceLoadSequenceRef.current + 1;
      workspaceLoadSequenceRef.current = loadSequence;
      workspaceLoadAbortRef.current?.abort();

      const controller = new AbortController();
      workspaceLoadAbortRef.current = controller;
      const isCurrentLoad = () =>
        workspaceLoadSequenceRef.current === loadSequence &&
        !controller.signal.aborted;
      let cachedSnapshotWasUsed = false;

      if (showLoading) {
        setIsLoadingWorkspace(true);
      }
      setIsRefreshingWorkspace(false);

      try {
        const cacheIdentity = currentUser?.user.id ?? 'anonymous';
        const cacheWorkspaceId = preferredWorkspaceId ?? 'default';
        const cacheViewId = preferredViewId ?? 'default';
        const workspaceCacheKey = buildWorkspaceCacheKey(
          cacheWorkspaceId,
          cacheViewId,
          cacheIdentity,
        );

        if (!options.force) {
          const cachedSnapshot = readCachedWorkspaceSnapshot(workspaceCacheKey);
          if (cachedSnapshot) {
            applyWorkspaceSnapshot(cachedSnapshot, preferredWorkspaceId);
            cachedSnapshotWasUsed = true;
            if (showLoading) {
              setIsLoadingWorkspace(false);
            }
            setIsRefreshingWorkspace(true);
          }
        }

        const workspaceList = await listWorkspaces({
          signal: controller.signal,
          workspaceId: null,
        });
        if (!isCurrentLoad()) {
          return;
        }

        const effectiveWorkspaceId =
          preferredWorkspaceId && workspaceList.some((candidate) => candidate.id === preferredWorkspaceId)
            ? preferredWorkspaceId
            : workspaceList[0]?.id ?? null;
        const workspaceRequestOptions = {
          workspaceId: effectiveWorkspaceId,
          signal: controller.signal,
        };

        if (!isCurrentLoad()) {
          return;
        }
        setCurrentWorkspaceId(effectiveWorkspaceId);
        window.localStorage.setItem(workspaceStorageKey, effectiveWorkspaceId ?? '');

        const [
          workspaceInfo,
          views,
          projectList,
          resolutions,
          templateSummaries,
          members,
          invitations,
        ] = await Promise.all([
          getWorkspace(workspaceRequestOptions),
          listSavedViews(workspaceRequestOptions),
          listProjects(workspaceRequestOptions),
          listArchiveResolutions(workspaceRequestOptions),
          listTaskTemplates(workspaceRequestOptions),
          listWorkspaceMembers(workspaceRequestOptions).catch(() => []),
          listWorkspaceInvitations(workspaceRequestOptions).catch(() => []),
        ]);
        if (!isCurrentLoad()) {
          return;
        }

        const resolvedWorkspaceList = workspaceList.some((candidate) => candidate.id === workspaceInfo.id)
          ? workspaceList
          : await listWorkspaces({
              signal: controller.signal,
              workspaceId: null,
            });
        const selectedViewId = pickSavedViewId(views, preferredViewId);
        const selectedView = views.find((view) => view.id === selectedViewId) ?? null;
        const workspaceIsSystemAllTasks = isSystemAllTasksWorkspace(workspaceInfo);
        const aggregateWorkspaces = workspaceIsSystemAllTasks
          ? resolvedWorkspaceList
          : [workspaceInfo];
        const aggregateTaskQuery = selectedView?.filter.archive === 'Archived' ||
          selectedView?.name.toLowerCase() === 'archive'
          ? { scope: 'Archive' as const }
          : { scope: 'Active' as const };
        const listAggregateTasks = async (query: Parameters<typeof listTaskItems>[0]) => {
          const taskLists = await Promise.all(
            aggregateWorkspaces.map((candidateWorkspace) =>
              listTaskItems(query, {
                signal: controller.signal,
                workspaceId: candidateWorkspace.id,
              }).catch((error) => {
                if (isAbortError(error)) {
                  throw error;
                }

                return [];
              })),
          );
          const taskItemsById = new Map<string, TaskItemSummaryResponse>();
          taskLists.flat().forEach((taskItem) => {
            taskItemsById.set(taskItem.id, taskItem);
          });

          return [...taskItemsById.values()];
        };
        const resolvedWorkspaceCacheKey = buildWorkspaceCacheKey(
          workspaceInfo.id,
          selectedViewId ?? 'default',
          cacheIdentity,
        );
        const [templateDetails, selectedTasks, viewCountResponses, allTasksForColors] = await Promise.all([
          Promise.all(templateSummaries.map(async (template) => {
            try {
              return await getTaskTemplate(template.id, workspaceRequestOptions);
            } catch (error) {
              if (isAbortError(error)) {
                throw error;
              }

              return null;
            }
          })).then((details) =>
            details.filter((template): template is TaskTemplateDetailResponse =>
              template !== null)),
          workspaceIsSystemAllTasks
            ? listAggregateTasks(aggregateTaskQuery)
            : selectedViewId
              ? listTaskItems({ viewId: selectedViewId }, workspaceRequestOptions)
              : Promise.resolve([]),
          listTaskViewCounts(
            views.map((view) => view.id),
            workspaceRequestOptions,
          ),
          workspaceIsSystemAllTasks
            ? listAggregateTasks({ archive: 'All' })
            : listTaskItems({ archive: 'All' }, workspaceRequestOptions),
        ]);
        if (!isCurrentLoad()) {
          return;
        }

        const colorOptions = mergeColorOptions(getTaskColors(allTasksForColors));
        const statuses = uniqueSorted(allTasksForColors.map((taskItem) => taskItem.status));
        const counts = Object.fromEntries(
          viewCountResponses.map((viewCount) => [viewCount.viewId, viewCount.count]),
        );

        setWorkspaces(resolvedWorkspaceList);
        setWorkspace(workspaceInfo);
        setSelectedWorkspaceId(workspaceInfo.id);
        setSavedViews(views);
        setProjects(projectList);
        setArchiveResolutions(resolutions);
        setWorkspaceMembers(members);
        setWorkspaceInvitations(invitations);
        setTemplates(templateDetails);
        setTaskColorOptions(colorOptions);
        setKnownStatuses(statuses);
        setCurrentViewId(selectedViewId);
        setTaskItems(selectedTasks);
        setViewCounts(counts);
        const snapshot = {
          archiveResolutions: resolutions,
          currentViewId: selectedViewId,
          knownStatuses: statuses,
          projects: projectList,
          savedViews: views,
          taskColorOptions: colorOptions,
          taskItems: selectedTasks,
          templates: templateDetails,
          viewCounts: counts,
          workspace: workspaceInfo,
          workspaceInvitations: invitations,
          workspaceMembers: members,
          workspaces: resolvedWorkspaceList,
        };
        writeCachedWorkspaceSnapshot(workspaceCacheKey, snapshot);
        writeCachedWorkspaceSnapshot(resolvedWorkspaceCacheKey, snapshot);
        setErrorMessage(null);

        if (selectedTaskId && !selectedTasks.some((taskItem) => taskItem.id === selectedTaskId)) {
          setSelectedTaskId(null);
          setSelectedTask(null);
        }
      } catch (error) {
        if (!isCurrentLoad() || isAbortError(error)) {
          return;
        }

        setErrorMessage(getErrorMessage(error));
      } finally {
        if (isCurrentLoad()) {
          if (showLoading && !cachedSnapshotWasUsed) {
            setIsLoadingWorkspace(false);
          }
          setIsRefreshingWorkspace(false);
        }
      }
    },
    [
      applyWorkspaceSnapshot,
      currentUser,
      currentViewId,
      selectedTaskId,
      selectedWorkspaceId,
    ],
  );

  useEffect(() => {
    void loadAuth();
  }, [loadAuth]);

  useEffect(() => {
    return () => {
      workspaceLoadAbortRef.current?.abort();
    };
  }, []);

  useEffect(() => {
    if (hasBootstrapped || isLoadingAuth) {
      return;
    }

    if (authOptions.requiresAuthentication && !currentUser) {
      setIsLoadingWorkspace(false);
      return;
    }

    setHasBootstrapped(true);
    void loadWorkspace(currentViewId);
  }, [
    authOptions.requiresAuthentication,
    currentUser,
    currentViewId,
    hasBootstrapped,
    isLoadingAuth,
    loadWorkspace,
  ]);

  useEffect(() => {
    window.localStorage.setItem(languageStorageKey, language);
  }, [language]);

  useEffect(() => {
    void pingBackend(false);
    const intervalId = window.setInterval(() => void pingBackend(true), 30000);

    return () => window.clearInterval(intervalId);
  }, [pingBackend]);

  useEffect(() => {
    if (!currentUser || temporarySessionIsActive || !selectedWorkspaceId) {
      return undefined;
    }

    let isDisposed = false;
    let reloadTimer: number | undefined;

    const scheduleWorkspaceReload = () => {
      if (reloadTimer) {
        window.clearTimeout(reloadTimer);
      }

      reloadTimer = window.setTimeout(() => {
        void loadWorkspace(currentViewId, selectedWorkspaceId, { force: true, silent: true });
      }, 250);
    };

    const handleLiveUpdate = (message: LiveUpdateMessage) => {
      if (message.actorUserId === currentUser.user.id) {
        return;
      }

      if (
        message.eventName === 'TaskShared' ||
        message.eventName === 'WorkspaceInviteAccepted'
      ) {
        void loadAuth();
      }

      if (
        !selectedWorkspaceId ||
        message.workspaceId === selectedWorkspaceId ||
        message.eventName === 'TaskShared' ||
        message.eventName === 'WorkspaceInviteAccepted'
      ) {
        scheduleWorkspaceReload();
      }

      if (selectedTaskId && message.taskItemId === selectedTaskId) {
        void getTaskItem(selectedTaskId, { workspaceId: selectedWorkspaceId })
          .then((taskItem) => {
            setSelectedTask(taskItem);
          })
          .catch(() => {
            setSelectedTaskId(null);
            setSelectedTask(null);
          });
      }
    };

    const subscription = startLiveUpdates(
      handleLiveUpdate,
      () => {
        if (isDisposed) {
          return;
        }

        const now = Date.now();
        if (now - liveConnectionToastAtRef.current > 15000) {
          liveConnectionToastAtRef.current = now;
          showToast(t('liveUpdatesDisconnected'), 'error');
        }
      },
    );
    if (selectedWorkspaceId) {
      void subscription.joinWorkspace(selectedWorkspaceId);
    }

    return () => {
      isDisposed = true;

      if (reloadTimer) {
        window.clearTimeout(reloadTimer);
      }

      void subscription.stop();
    };
  }, [
    currentUser,
    currentViewId,
    loadAuth,
    loadWorkspace,
    selectedTaskId,
    selectedWorkspaceId,
    showToast,
    t,
    temporarySessionIsActive,
  ]);

  useEffect(() => {
    if (mode !== 'tasks' || !selectedTaskId) {
      setSelectedTask(null);
      return;
    }

    let requestIsStale = false;
    const controller = new AbortController();
    setIsLoadingDetail(true);

    getTaskItem(selectedTaskId, {
      workspaceId: selectedWorkspaceId,
      signal: controller.signal,
    })
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
        if (!requestIsStale && !isAbortError(error)) {
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
      controller.abort();
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

  const handleStartSidebarResize = (event: MouseEvent<HTMLButtonElement>) => {
    event.preventDefault();

    const startX = event.clientX;
    const startWidth = sidebarWidth;

    const handlePointerMove = (moveEvent: globalThis.MouseEvent) => {
      const nextWidth = clamp(
        startWidth + moveEvent.clientX - startX,
        minSidebarWidth,
        maxSidebarWidth,
      );
      setSidebarWidth(nextWidth);
      window.localStorage.setItem(sidebarWidthStorageKey, nextWidth.toString());
    };

    const handlePointerUp = () => {
      window.removeEventListener('mousemove', handlePointerMove);
      window.removeEventListener('mouseup', handlePointerUp);
    };

    window.addEventListener('mousemove', handlePointerMove);
    window.addEventListener('mouseup', handlePointerUp);
  };

  const handleCreateWorkspace = async (name: string) => {
    try {
      setCurrentWorkspaceId(null);
      const created = await createWorkspace({ name: name.trim() });
      setWorkspaces((currentWorkspaces) => [...currentWorkspaces, created]);
      handleSelectWorkspace(created.id);
    } catch (error) {
      const message = getErrorMessage(error);
      setErrorMessage(message);
      showToast(message, 'error');
      throw error;
    }
  };

  const handleCreateWorkspaceInvitation = async (
    requestBody: CreateWorkspaceInvitationRequest,
  ) => {
    try {
      const created = await createWorkspaceInvitation(requestBody);
      setWorkspaceInvitations((currentInvitations) => [created, ...currentInvitations]);
      setErrorMessage(null);
      return created;
    } catch (error) {
      const message = getErrorMessage(error);
      setErrorMessage(message);
      showToast(message, 'error');
      throw error;
    }
  };

  const handleAcceptShareLink = useCallback(async (token: string) => {
    try {
      const accepted = await acceptShareLink({ token });
      await loadAuth();
      await loadWorkspace(null, accepted.workspaceId || selectedWorkspaceId, { force: true });
      showToast(t('workspaceInviteAccepted'), 'info');
      setErrorMessage(null);
    } catch (error) {
      const message = getErrorMessage(error);
      setErrorMessage(message);
      showToast(message, 'error');
      throw error;
    }
  }, [loadAuth, loadWorkspace, selectedWorkspaceId, showToast, t]);

  useEffect(() => {
    const searchParams = new URLSearchParams(window.location.search);
    const inviteToken = searchParams.get('shareToken') ?? searchParams.get('workspaceInvite');

    if (!currentUser || !inviteToken || processedWorkspaceInviteTokenRef.current === inviteToken) {
      return;
    }

    processedWorkspaceInviteTokenRef.current = inviteToken;
    void handleAcceptShareLink(inviteToken)
      .then(() => {
        const nextUrl = new URL(window.location.href);
        nextUrl.searchParams.delete('shareToken');
        nextUrl.searchParams.delete('workspaceInvite');
        window.history.replaceState({}, '', nextUrl.toString());
      })
      .catch(() => {
        processedWorkspaceInviteTokenRef.current = null;
      });
  }, [currentUser, handleAcceptShareLink]);

  const handleAcceptIncomingWorkspaceInvitation = async (id: string) => {
    try {
      await acceptIncomingWorkspaceInvitation(id);
      setIncomingWorkspaceInvitations((currentInvitations) =>
        currentInvitations.filter((invitation) => invitation.id !== id),
      );
      await loadAuth();
      await loadWorkspace(null, selectedWorkspaceId, { force: true });
      showToast(t('workspaceInviteAccepted'), 'info');
      setErrorMessage(null);
    } catch (error) {
      const message = getErrorMessage(error);
      setErrorMessage(message);
      showToast(message, 'error');
      throw error;
    }
  };

  const handleDeclineIncomingWorkspaceInvitation = async (id: string) => {
    try {
      await declineIncomingWorkspaceInvitation(id);
      setIncomingWorkspaceInvitations((currentInvitations) =>
        currentInvitations.filter((invitation) => invitation.id !== id),
      );
      showToast(t('workspaceInviteDeclined'), 'info');
      setErrorMessage(null);
    } catch (error) {
      const message = getErrorMessage(error);
      setErrorMessage(message);
      showToast(message, 'error');
      throw error;
    }
  };

  const handleLeaveTaskShare = async (shareId: string) => {
    try {
      await leaveTaskShare(shareId);
      setIncomingTaskShares((currentShares) =>
        currentShares.filter((share) => share.shareId !== shareId),
      );
      if (selectedTask?.shares.some((share) => share.id === shareId)) {
        setSelectedTaskId(null);
        setSelectedTask(null);
      }
      await loadAuth();
      await loadWorkspace(currentViewId, selectedWorkspaceId, { force: true });
      showToast(t('taskShareLeft'), 'info');
      setErrorMessage(null);
    } catch (error) {
      const message = getErrorMessage(error);
      setErrorMessage(message);
      showToast(message, 'error');
      throw error;
    }
  };

  const handleRevokeWorkspaceInvitation = async (id: string) => {
    try {
      await revokeWorkspaceInvitation(id);
      setWorkspaceInvitations((currentInvitations) =>
        currentInvitations.filter((invitation) => invitation.id !== id),
      );
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  const handleRemoveWorkspaceMember = async (userId: string) => {
    try {
      await removeWorkspaceMember(userId);
      setWorkspaceMembers((currentMembers) =>
        currentMembers.filter((member) => member.userId !== userId),
      );
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  const handleUpdateWorkspaceMemberRole = async (
    userId: string,
    requestBody: UpdateWorkspaceMemberRequest,
  ) => {
    try {
      const updated = await updateWorkspaceMemberRole(userId, requestBody);
      setWorkspaceMembers((currentMembers) =>
        currentMembers.map((member) =>
          member.userId === updated.userId ? updated : member,
        ),
      );
      setErrorMessage(null);
      return updated;
    } catch (error) {
      const message = getErrorMessage(error);
      setErrorMessage(message);
      showToast(message, 'error');
      throw error;
    }
  };

  const handleLeaveWorkspaceAccess = async (workspaceId: string) => {
    const workspaceToLeave = workspaces.find((candidate) => candidate.id === workspaceId);

    try {
      if (workspaceToLeave && isTaskShareWorkspace(workspaceToLeave)) {
        await leaveWorkspaceTaskShares(workspaceId);
        setIncomingTaskShares((currentShares) =>
          currentShares.filter((share) => share.workspaceId !== workspaceId),
        );
      } else {
        setCurrentWorkspaceId(workspaceId);
        await leaveCurrentWorkspace();
      }

      if (selectedWorkspaceId === workspaceId) {
        setSelectedWorkspaceId(null);
        setSelectedTaskId(null);
        setSelectedTask(null);
      }

      await loadAuth();
      await loadWorkspace(null, null, { force: true });
      showToast(t('workspaceLeft'), 'info');
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  const handleAuthenticated = async (userState: CurrentUserResponse) => {
    setCurrentUser(userState);
    setTemporarySessionIsActive(isTemporarySession());
    const workspaceId = userState.workspaces[0]?.id ?? null;
    setSelectedWorkspaceId(workspaceId);
    setSelectedTaskId(null);
    setSelectedTask(null);
    setHasBootstrapped(true);
    await loadWorkspace(null, workspaceId);
  };

  const handleLogin = async (requestBody: LoginUserRequest) => {
    try {
      const loggedIn = await loginUser(requestBody);
      await handleAuthenticated(loggedIn);
      showToast(t('authLoggedIn'), 'info');
      setErrorMessage(null);
    } catch (error) {
      const message = getErrorMessage(error);
      setErrorMessage(message);
      showToast(message, 'error');
      throw error;
    }
  };

  const handleRegister = async (requestBody: RegisterUserRequest) => {
    try {
      const registered = await registerUser(requestBody);

      if (registered.emailConfirmationRequired) {
        showToast(t('emailConfirmationSent'), 'info');
        setErrorMessage(null);
        return registered;
      }

      const loggedIn = await loginUser({
        email: requestBody.email,
        password: requestBody.password,
        deviceName: 'web browser',
      });
      await handleAuthenticated(loggedIn);
      showToast(t('authRegistered'), 'info');
      setErrorMessage(null);
      return registered;
    } catch (error) {
      const message = getErrorMessage(error);
      setErrorMessage(message);
      showToast(message, 'error');
      throw error;
    }
  };

  const handleDevelopmentLogin = async () => {
    try {
      const loggedIn = await developmentLogin();
      await handleAuthenticated(loggedIn);
      showToast(t('authLoggedIn'), 'info');
      setErrorMessage(null);
    } catch (error) {
      const message = getErrorMessage(error);
      setErrorMessage(message);
      showToast(message, 'error');
      throw error;
    }
  };

  const handleGuestLogin = async () => {
    try {
      const loggedIn = await guestLogin();
      await handleAuthenticated(loggedIn);
      setTemporarySessionIsActive(true);
      showToast(t('guestModeToast'), 'warning');
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
      throw error;
    }
  };

  const handleLogout = async () => {
    try {
      await logoutUser();
      setCurrentUser(null);
      setTemporarySessionIsActive(false);
      setCurrentWorkspaceId(null);
      setSelectedWorkspaceId(null);
      setWorkspace(null);
      setWorkspaces([]);
      setWorkspaceMembers([]);
      setWorkspaceInvitations([]);
      setIncomingWorkspaceInvitations([]);
      setIncomingTaskShares([]);
      setSavedViews([]);
      setProjects([]);
      setTaskItems([]);
      setSelectedTaskId(null);
      setSelectedTask(null);
      window.localStorage.removeItem(workspaceStorageKey);
      setHasBootstrapped(false);

      if (!authOptions.requiresAuthentication) {
        await loadWorkspace(null, null);
      }

      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
      throw error;
    }
  };

  const handleCreateProject = async (name: string, color?: string | null) => {
    try {
      const created = await createProject({
        name: name.trim(),
        color: color?.trim() || null,
      });
      setProjects((currentProjects) => [...currentProjects, created]);
    } catch (error) {
      const message = getErrorMessage(error);
      setErrorMessage(message);
      showToast(message, 'error');
      throw error;
    }
  };

  const handleOpenTemplates = () => {
    const nextMode: WorkspaceMode = mode === 'templates' ? 'tasks' : 'templates';

    setMode(nextMode);
    setSelectedTaskId(null);
    updateUrl(nextMode, nextMode === 'templates' ? null : currentViewId);
  };

  const handleToggleSettings = () => {
    setAccountIsOpen(false);
    setSettingsIsOpen((isOpen) => !isOpen);
  };

  const handleToggleAccount = () => {
    setSettingsIsOpen(false);
    setAccountIsOpen((isOpen) => !isOpen);
  };

  const handleCreateTaskItem = async (
    title: string,
    options: Partial<CreateTaskItemRequest> = {},
  ) => {
    if (!workspace?.id) {
      const message = t('createBoardBeforeTasks');
      showToast(message, 'error');
      return null;
    }

    try {
      const created = await createTaskItem({
        title,
        projectId: options.projectId ?? null,
        category: options.category ?? null,
        taskTemplateId: options.taskTemplateId ?? null,
      }, {
        workspaceId: workspace.id,
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
      return created;
    } catch (error) {
      const message = getErrorMessage(error);
      showToast(message, 'error');
      return null;
    }
  };

  const handleCopyTaskItemsToWorkspace = async (
    taskItemIds: string[],
    destinationWorkspaceId: string,
  ) => {
    if (taskItemIds.length === 0) {
      return;
    }

    try {
      await copyTaskItems({
        taskItemIds,
        destinationWorkspaceId,
      });

      if (destinationWorkspaceId === selectedWorkspaceId) {
        await loadWorkspace(currentViewId, selectedWorkspaceId, { force: true });
      }

      showToast(t('tasksCopied'));
    } catch (error) {
      const message = getErrorMessage(error);
      setErrorMessage(message);
      showToast(message, 'error');
      throw error;
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
      const message = getErrorMessage(error);
      setErrorMessage(message);
      showToast(message, 'error');
      throw error;
    }
  };

  const handleCreateTaskShareLink = async (
    taskItemId: string,
    requestBody: CreateTaskShareRequest,
  ) => {
    try {
      const created = await createTaskShareLink(taskItemId, requestBody);
      const updated = await getTaskItem(taskItemId);
      setSelectedTask((currentTask) =>
        currentTask?.id === updated.id ? updated : currentTask,
      );
      setTaskItems((currentItems) =>
        currentItems.map((taskItem) =>
          taskItem.id === updated.id ? updated : taskItem,
        ),
      );
      await loadWorkspace(currentViewId);
      setErrorMessage(null);
      return created;
    } catch (error) {
      const message = getErrorMessage(error);
      setErrorMessage(message);
      showToast(message, 'error');
      throw error;
    }
  };

  const handleCreateTaskShareLinks = async (
    requestBody: CreateTaskShareLinkRequest,
  ) => {
    try {
      const created = await createTaskShareLinks(requestBody);
      await loadWorkspace(currentViewId);
      setErrorMessage(null);
      return created;
    } catch (error) {
      const message = getErrorMessage(error);
      setErrorMessage(message);
      showToast(message, 'error');
      throw error;
    }
  };

  const handleRevokeTaskShare = async (taskItemId: string, shareId: string) => {
    try {
      const updated = await revokeTaskShare(taskItemId, shareId);
      setSelectedTask((currentTask) =>
        currentTask?.id === updated.id ? updated : currentTask,
      );
      setTaskItems((currentItems) =>
        currentItems.map((taskItem) =>
          taskItem.id === updated.id ? updated : taskItem,
        ),
      );
      setErrorMessage(null);
    } catch (error) {
      const message = getErrorMessage(error);
      setErrorMessage(message);
      showToast(message, 'error');
      throw error;
    }
  };

  const handleUpdateTaskShareRole = async (
    taskItemId: string,
    shareId: string,
    requestBody: UpdateTaskShareRequest,
  ) => {
    try {
      const updated = await updateTaskShareRole(taskItemId, shareId, requestBody);
      setSelectedTask((currentTask) =>
        currentTask?.id === updated.id ? updated : currentTask,
      );
      setTaskItems((currentItems) =>
        currentItems.map((taskItem) =>
          taskItem.id === updated.id ? updated : taskItem,
        ),
      );
      setErrorMessage(null);
      return updated;
    } catch (error) {
      const message = getErrorMessage(error);
      setErrorMessage(message);
      showToast(message, 'error');
      throw error;
    }
  };

  const handleUpdateTaskItems = async (
    taskItemIds: string[],
    requestBody: UpdateTaskItemRequest,
  ) => {
    if (taskItemIds.length === 0) {
      return;
    }

    try {
      await Promise.all(taskItemIds.map((taskItemId) => updateTaskItem(taskItemId, requestBody)));
      await loadWorkspace(currentViewId);
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  const handleUpdateFieldValues = async (fieldValues: FieldValueMap) => {
    await handleUpdateTaskItem({ fieldValues });
  };

  const handleAddTimelineEntry = async (
    note: string,
    fieldValues?: FieldValueMap,
  ) => {
    if (!selectedTask) {
      return;
    }

    try {
      const updated = await addTaskTimelineEntry(selectedTask.id, {
        note,
        ...(fieldValues ? { fieldValues } : {}),
      });
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

  const handleUpdateTimelineEntry = async (
    entryId: string,
    note: string | null,
    fieldValues?: FieldValueMap,
  ) => {
    if (!selectedTask) {
      return;
    }

    try {
      const updated = await updateTaskTimelineEntry(selectedTask.id, entryId, {
        note,
        ...(fieldValues ? { fieldValues } : {}),
      });
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

  const handleDeleteProject = async (projectId: string) => {
    try {
      const deletedProject = projects.find((project) => project.id === projectId) ?? null;
      const remainingProjects = projects.filter((project) => project.id !== projectId);
      await deleteProject(projectId);
      setProjects(remainingProjects);
      setTaskItems((currentItems) =>
        currentItems.map((taskItem) => {
          const nextCategory = deletedProject
            ? joinTaskCategories(
              splitTaskCategories(taskItem.category).filter((category) =>
                category.toLowerCase() !== deletedProject.name.toLowerCase()),
            )
            : taskItem.category;
          const nextProjectId = taskItem.projectId === projectId
            ? getPrimaryProjectIdForCategories(nextCategory, remainingProjects)
            : taskItem.projectId;

          return {
            ...taskItem,
            projectId: nextProjectId,
            category: nextCategory,
          };
        }),
      );
      await loadWorkspace(currentViewId, selectedWorkspaceId, { force: true });
      setErrorMessage(null);
    } catch (error) {
      const message = getErrorMessage(error);
      setErrorMessage(message);
      showToast(message, 'error');
      throw error;
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

  const handleReopenTaskItems = async (taskItemIds: string[], note?: string) => {
    if (taskItemIds.length === 0) {
      return;
    }

    try {
      await reopenTaskItems({
        taskItemIds,
        note: note?.trim() || null,
      });
      setSelectedTaskId(null);
      setSelectedTask(null);
      await loadWorkspace(currentViewId, selectedWorkspaceId, { force: true });
      showToast(t('tasksUnarchived'));
    } catch (error) {
      const message = getErrorMessage(error);
      setErrorMessage(message);
      showToast(message, 'error');
      throw error;
    }
  };

  const handleDeleteTaskItemsPermanently = async (taskItemIds: string[]) => {
    if (taskItemIds.length === 0) {
      return;
    }

    try {
      await deleteTaskItemsPermanently({ taskItemIds });
      setSelectedTaskId(null);
      setSelectedTask(null);
      await loadWorkspace(currentViewId, selectedWorkspaceId, { force: true });
      showToast(t('tasksDeletedPermanently'));
    } catch (error) {
      const message = getErrorMessage(error);
      setErrorMessage(message);
      showToast(message, 'error');
      throw error;
    }
  };

  const handleSaveTemplate = async (
    id: string | null,
    name: string,
    fields: UpsertFieldDefinitionRequest[],
    layout: TaskTemplateLayoutResponse,
  ): Promise<TaskTemplateDetailResponse | null> => {
    try {
      const savedTemplate = id
        ? await updateTaskTemplate(id, { name, fields, layout })
        : await createTaskTemplate({ name, fields, layout });

      setTemplates((currentTemplates) => {
        const existingIndex = currentTemplates.findIndex(
          (template) => template.id === savedTemplate.id,
        );

        if (existingIndex >= 0) {
          return currentTemplates.map((template) =>
            template.id === savedTemplate.id ? savedTemplate : template);
        }

        return [...currentTemplates, savedTemplate]
          .sort((first, second) => first.name.localeCompare(second.name));
      });

      setSelectedTask((currentTask) =>
        currentTask?.taskTemplateId === savedTemplate.id
          ? { ...currentTask, template: savedTemplate }
          : currentTask);

      void loadWorkspace(currentViewId, selectedWorkspaceId, {
        force: true,
        silent: true,
      });
      setErrorMessage(null);
      return savedTemplate;
    } catch (error) {
      const message = getErrorMessage(error);
      setErrorMessage(message);
      showToast(message, 'error');
      return null;
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

  const handleCreateArchiveResolution = async (
    requestBody: CreateArchiveResolutionRequest,
  ) => {
    try {
      const created = await createArchiveResolution(requestBody);
      setArchiveResolutions((currentReasons) => [...currentReasons, created]);
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  const handleUpdateArchiveResolution = async (
    id: string,
    requestBody: UpdateArchiveResolutionRequest,
  ) => {
    try {
      const updated = await updateArchiveResolution(id, requestBody);
      setArchiveResolutions((currentReasons) =>
        currentReasons.map((reason) => reason.id === updated.id ? updated : reason),
      );
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  const handleDeleteArchiveResolution = async (id: string) => {
    try {
      await deleteArchiveResolution(id);
      setArchiveResolutions((currentReasons) =>
        currentReasons.filter((reason) => reason.id !== id),
      );
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  const handleSaveStatusOptions = (statuses: string[]) => {
    const normalizedStatuses = uniqueSorted(statuses);
    setConfiguredStatuses(normalizedStatuses);
    window.localStorage.setItem(
      statusOptionsStorageKey,
      JSON.stringify(normalizedStatuses),
    );
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
      const message = getErrorMessage(error);
      setErrorMessage(message);
      showToast(message, 'error');
      throw error;
    }
  };

  const handleUpdateWorkspaceById = async (
    workspaceId: string,
    requestBody: UpdateWorkspaceRequest,
  ) => {
    try {
      const updated = await updateWorkspaceById(workspaceId, requestBody);
      setWorkspaces((currentWorkspaces) =>
        currentWorkspaces.map((currentWorkspace) =>
          currentWorkspace.id === updated.id ? updated : currentWorkspace,
        ),
      );
      setWorkspace((currentWorkspace) =>
        currentWorkspace?.id === updated.id ? updated : currentWorkspace,
      );
      await loadAuth();
      setErrorMessage(null);
    } catch (error) {
      const message = getErrorMessage(error);
      setErrorMessage(message);
      showToast(message, 'error');
      throw error;
    }
  };

  const handleDeleteWorkspace = async (workspaceId: string) => {
    try {
      const workspaceToDelete = workspaces.find((currentWorkspace) => currentWorkspace.id === workspaceId);

      if (workspaceToDelete && isSystemAllTasksWorkspace(workspaceToDelete)) {
        showToast(t('systemBoardCannotBeDeleted'), 'warning');
        return;
      }

      await deleteWorkspace(workspaceId);
      setWorkspaces((currentWorkspaces) =>
        currentWorkspaces.filter((currentWorkspace) => currentWorkspace.id !== workspaceId),
      );
      if (selectedWorkspaceId === workspaceId) {
        setSelectedWorkspaceId(null);
        setSelectedTaskId(null);
        setSelectedTask(null);
      }
      await loadAuth();
      await loadWorkspace(null, null, { force: true });
      showToast(t('workspaceDeleted'), 'info');
      setErrorMessage(null);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
      throw error;
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
      const message = getErrorMessage(error);
      setErrorMessage(message);
      showToast(message, 'error');
      throw error;
    }
  };

  return (
    <main
      className="app-shell"
      data-sidebar-collapsed={sidebarIsCollapsed}
      style={{ '--sidebar-width': `${sidebarWidth}px` } as CSSProperties}
    >
      <Sidebar
        accountNotificationCount={accountNotificationCount}
        counts={viewCounts}
        currentUser={currentUser}
        currentViewId={currentViewId}
        language={language}
        mode={mode}
        onCreateWorkspace={handleCreateWorkspace}
        onDeleteWorkspace={handleDeleteWorkspace}
        onLeaveWorkspaceAccess={handleLeaveWorkspaceAccess}
        onOpenAccount={handleToggleAccount}
        onOpenSettings={handleToggleSettings}
        onOpenTemplates={handleOpenTemplates}
        onRefresh={() => void loadWorkspace(currentViewId, selectedWorkspaceId, { force: true })}
        onResizeStart={handleStartSidebarResize}
        onSelectWorkspace={handleSelectWorkspace}
        onSelectView={handleSelectSavedView}
        onToggleSidebar={() => setSidebarIsCollapsed((isCollapsed) => !isCollapsed)}
        onUpdateWorkspace={handleUpdateWorkspaceById}
        connectionStatus={connectionStatus}
        lastPingedAt={lastPingedAt}
        savedViews={savedViews}
        sidebarIsCollapsed={sidebarIsCollapsed}
        templateCount={templates.length}
        temporarySessionIsActive={temporarySessionIsActive}
        t={t}
        workspace={workspace}
        workspaces={workspaces}
      />

      <section className="workspace" aria-label="Task workspace">
        {isLoadingAuth ? (
          <section className="auth-gate" aria-label={t('account')}>
            <p className="detail-kicker">DumpTether</p>
            <h2>{t('loadingAccount')}</h2>
          </section>
        ) : authOptions.requiresAuthentication && !currentUser ? (
          <AuthPanel
            authOptions={authOptions}
            currentUser={currentUser}
            isLoading={isLoadingAuth}
            onDevelopmentLogin={handleDevelopmentLogin}
            onGuestLogin={handleGuestLogin}
            onLogin={handleLogin}
            onRegister={handleRegister}
            temporarySessionIsActive={temporarySessionIsActive}
            t={t}
            variant="gate"
          />
        ) : mode === 'templates' ? (
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
            currentUserEmail={currentUser?.user.email ?? null}
            colorOptions={taskColorOptions}
            isLoading={isLoadingWorkspace}
            isLoadingDetail={isLoadingDetail}
            isRefreshing={isRefreshingWorkspace}
            onAddTimelineEntry={handleAddTimelineEntry}
            onArchive={handleArchiveTaskItem}
            onArchiveTaskItems={handleArchiveTaskItems}
            onCloseArchiveDialog={() => setArchiveDialogIsOpen(false)}
            onCopyTaskItemsToWorkspace={handleCopyTaskItemsToWorkspace}
            onCreateTaskItem={handleCreateTaskItem}
            onCreateProject={handleCreateProject}
            onCreateTaskShareLink={handleCreateTaskShareLink}
            onCreateTaskShareLinks={handleCreateTaskShareLinks}
            onCreateWorkspaceInvitation={handleCreateWorkspaceInvitation}
            onDeleteTimelineEntry={handleDeleteTimelineEntry}
            onDeleteProject={handleDeleteProject}
            onOpenArchiveDialog={() => setArchiveDialogIsOpen(true)}
            onReopen={handleReopenTaskItem}
            onReopenTaskItems={handleReopenTaskItems}
            onDeleteTaskItemsPermanently={handleDeleteTaskItemsPermanently}
            onRevokeTaskShare={handleRevokeTaskShare}
            onRevokeWorkspaceInvitation={handleRevokeWorkspaceInvitation}
            onRemoveWorkspaceMember={handleRemoveWorkspaceMember}
            onSelectTaskItem={(id) => {
              setSelectedTaskId(id);
            }}
            onCloseTaskItem={() => {
              setSelectedTaskId(null);
              setSelectedTask(null);
            }}
            onUpdateFieldValues={handleUpdateFieldValues}
            onUpdateTaskShareRole={handleUpdateTaskShareRole}
            onUpdateTaskItems={handleUpdateTaskItems}
            onUpdateTaskItem={handleUpdateTaskItem}
            onUpdateTimelineEntry={handleUpdateTimelineEntry}
            onUpdateProject={handleUpdateProject}
            onUpdateWorkspace={handleUpdateWorkspace}
            onUpdateWorkspaceMemberRole={handleUpdateWorkspaceMemberRole}
            onShowToast={showToast}
            projects={projects}
            selectedTask={selectedTask}
            selectedTaskId={selectedTaskId}
            statusOptions={statusOptions}
            taskItems={taskItems}
            templates={templates}
            t={t}
            workspaceInvitations={workspaceInvitations}
            workspaceMembers={workspaceMembers}
            workspace={workspace}
            workspaces={workspaces}
          />
        )}
      </section>

      {settingsIsOpen ? (
        <SettingsPanel
          archiveResolutions={archiveResolutions}
          configuredStatuses={configuredStatuses}
          language={language}
          onChangeLanguage={setLanguage}
          onCreateArchiveResolution={handleCreateArchiveResolution}
          onDeleteArchiveResolution={handleDeleteArchiveResolution}
          onSaveStatusOptions={handleSaveStatusOptions}
          onUpdateArchiveResolution={handleUpdateArchiveResolution}
          onClose={() => setSettingsIsOpen(false)}
          t={t}
        />
      ) : null}
      {accountIsOpen ? (
        <AccountPanel
          authOptions={authOptions}
          currentUser={currentUser}
          incomingTaskShares={incomingTaskShares}
          incomingWorkspaceInvitations={incomingWorkspaceInvitations}
          isLoadingAuth={isLoadingAuth}
          onAcceptIncomingWorkspaceInvitation={handleAcceptIncomingWorkspaceInvitation}
          onClose={() => setAccountIsOpen(false)}
          onDeclineIncomingWorkspaceInvitation={handleDeclineIncomingWorkspaceInvitation}
          onDevelopmentLogin={handleDevelopmentLogin}
          onGuestLogin={handleGuestLogin}
          onLeaveTaskShare={handleLeaveTaskShare}
          onLogin={handleLogin}
          onLogout={handleLogout}
          onRegister={handleRegister}
          temporarySessionIsActive={temporarySessionIsActive}
          t={t}
        />
      ) : null}
      <ToastStack onDismiss={dismissToast} toasts={toasts} />
    </main>
  );
}

function TaskBoard({
  archiveDialogIsOpen,
  archiveResolutions,
  colorOptions,
  currentView,
  currentUserEmail,
  isLoading,
  isLoadingDetail,
  isRefreshing,
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
  taskItems,
  templates,
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
  isLoading: boolean;
  isLoadingDetail: boolean;
  isRefreshing: boolean;
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
    options?: Partial<CreateTaskItemRequest>,
  ) => Promise<TaskItemDetailResponse | null>;
  onCreateWorkspaceInvitation: (
    requestBody: CreateWorkspaceInvitationRequest,
  ) => Promise<WorkspaceInvitationResponse>;
  onDeleteProject: (projectId: string) => Promise<void>;
  onDeleteTimelineEntry: (entryId: string) => Promise<void>;
  onOpenArchiveDialog: () => void;
  onReopen: (note?: string) => Promise<void>;
  onReopenTaskItems: (taskItemIds: string[], note?: string) => Promise<void>;
  onDeleteTaskItemsPermanently: (taskItemIds: string[]) => Promise<void>;
  onRemoveWorkspaceMember: (userId: string) => Promise<void>;
  onRevokeTaskShare: (taskItemId: string, shareId: string) => Promise<void>;
  onRevokeWorkspaceInvitation: (id: string) => Promise<void>;
  onCloseTaskItem: () => void;
  onSelectTaskItem: (id: string) => void;
  onUpdateFieldValues: (fieldValues: FieldValueMap) => Promise<void>;
  onUpdateProject: (id: string, requestBody: UpdateProjectRequest) => Promise<void>;
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
  taskItems: TaskItemSummaryResponse[];
  templates: TaskTemplateDetailResponse[];
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
  const canManageSharing = currentUserOwnsWorkspace && !workspaceIsTaskShareOnly;
  const canManageWorkspaceMetadata = currentUserOwnsWorkspace && !workspaceIsTaskShareOnly;
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
  const longPressTimerRef = useRef<number | null>(null);
  const longPressHandledRef = useRef(false);
  const visibleTaskItems = useMemo(
    () => applyTaskWallFilters(taskItems, filters, currentUserEmail, projects),
    [currentUserEmail, filters, projects, taskItems],
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
    if (!hasWorkspace) {
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

    setDraftTaskIsOpen(true);
  }, [canCreateTask, draftTaskIsOpen, focusedTaskItem, hasWorkspace, onShowToast, t]);

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
          currentView={currentView}
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
            onCancel={() => setDraftTaskIsOpen(false)}
            onCreateTaskItem={onCreateTaskItem}
            onCreated={(createdTask) => {
              setDraftTaskIsOpen(false);
              onSelectTaskItem(createdTask.id);
            }}
            projects={projects}
            selectedProjectId={selectedProjectIds[0] ?? ''}
            t={t}
            templates={templates}
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
                    onSelectTaskItem(taskItem.id);
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
                      colorOptions={colorOptions}
                      canManageSharing={canManageSharing}
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
    </section>
  );
}

export default App;
