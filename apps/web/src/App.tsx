import {
  type CSSProperties,
  type MouseEvent,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
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
  getErrorMessage,
  getInitialLanguage,
  getInitialMode,
  getInitialViewId,
  getInitialWorkspaceId,
  isAbortError,
  isOwnerRole,
  isSystemAllTasksWorkspace,
  isTaskShareWorkspace,
  pickSavedViewId,
  readStoredStringList,
  updateUrl,
} from './appUtils';
import {
  addTaskTimelineEntry,
  acceptShareLink,
  acceptIncomingWorkspaceInvitation,
  archiveTaskItem,
  checkHealth,
  connectCloudSyncAccount,
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
  disconnectCloudSyncAccount,
  declineIncomingWorkspaceInvitation,
  developmentLogin,
  getCloudSyncAccount,
  guestLogin,
  getTaskItem,
  getTaskTemplate,
  getWorkspace,
  importTaskTemplateFromTask,
  leaveCurrentWorkspace,
  leaveTaskShare,
  leaveWorkspaceTaskShares,
  listArchiveResolutions,
  listProjects,
  listSavedViews,
  listWorkspaceSyncRoots,
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
  revokeAuthSession,
  revokeTaskShare,
  revokeWorkspaceInvitation,
  setCurrentWorkspaceId,
  isTemporarySession,
  syncWorkspaceWithCloud,
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
import { loadAuthSession } from './features/auth/authSession';
import { Sidebar } from './features/navigation/Sidebar';
import {
  AccountPanel,
  AuthPanel,
  SettingsPanel,
} from './features/settings/AccountSettingsPanels';
import { TemplatesPage } from './features/templates/TemplatesPage';
import { TaskBoard } from './features/task-wall/TaskBoard';
import { type CreateTaskItemOptions } from './features/task-wall/taskWallTypes';
import { startLiveUpdates, type LiveUpdateMessage } from './liveUpdates';
import { type Language, type Translate, translate } from './localization';
import {
  getPrimaryProjectIdForCategories,
  getTaskColors,
  joinTaskCategories,
  mergeColorOptions,
  splitTaskCategories,
  uniqueSorted,
} from './taskUtils';
import type {
  AuthClientOptionsResponse,
  ArchiveResolutionResponse,
  ArchiveTaskItemRequest,
  AuthSessionListItemResponse,
  CloudSyncAccountResponse,
  ConnectCloudAccountRequest,
  CurrentUserResponse,
  CreateArchiveResolutionRequest,
  CreateTaskShareRequest,
  CreateTaskShareLinkRequest,
  CreateWorkspaceInvitationRequest,
  FieldValueMap,
  LoginUserRequest,
  ProjectResponse,
  RegisterUserRequest,
  SavedViewResponse,
  TaskItemDetailResponse,
  TaskItemSummaryResponse,
  SyncWorkspaceWithCloudRequest,
  SyncWorkspaceWithCloudResponse,
  SyncRootResponse,
  TaskShareInboxResponse,
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
  const [syncRoots, setSyncRoots] = useState<SyncRootResponse[]>([]);
  const [templates, setTemplates] = useState<TaskTemplateDetailResponse[]>([]);
  const [importedTemplateSourceIds, setImportedTemplateSourceIds] = useState<string[]>([]);
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
  const [selectedTaskWorkspaceId, setSelectedTaskWorkspaceId] = useState<string | null>(null);
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
  const [authSessions, setAuthSessions] = useState<AuthSessionListItemResponse[]>([]);
  const [incomingWorkspaceInvitations, setIncomingWorkspaceInvitations] =
    useState<WorkspaceInvitationInboxResponse[]>([]);
  const [incomingTaskShares, setIncomingTaskShares] = useState<TaskShareInboxResponse[]>([]);
  const processedWorkspaceInviteTokenRef = useRef<string | null>(null);
  const [localDesktopSessionIsActive, setLocalDesktopSessionIsActive] = useState(false);
  const [cloudSyncAccount, setCloudSyncAccount] = useState<CloudSyncAccountResponse | null>(null);
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
  const cloudSyncFailureToastAtRef = useRef(0);
  const cloudSyncInFlightRef = useRef<Set<string>>(new Set());
  const cloudSyncRetryAfterRef = useRef<Map<string, number>>(new Map());
  const syncRootsRef = useRef<SyncRootResponse[]>([]);
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

  const applyTaskUpdate = useCallback((updated: TaskItemDetailResponse) => {
    setSelectedTask((currentTask) =>
      currentTask?.id === updated.id ? updated : currentTask,
    );
    setTaskItems((currentItems) =>
      currentItems.map((taskItem) =>
        taskItem.id === updated.id ? updated : taskItem,
      ),
    );
  }, []);

  useEffect(() => {
    syncRootsRef.current = syncRoots;
  }, [syncRoots]);

  const currentView = useMemo(
    () => savedViews.find((view) => view.id === currentViewId) ?? null,
    [currentViewId, savedViews],
  );
  const cleanupWorkspaces = useMemo(
    () => workspaces.filter((candidate) =>
      !isSystemAllTasksWorkspace(candidate) &&
      currentUser?.workspaces.some((membership) =>
        membership.id === candidate.id &&
        membership.accessKind !== 'TaskShare' &&
        isOwnerRole(membership.role)) === true),
    [currentUser, workspaces],
  );
  const cloudLinkedWorkspaceIds = useMemo(
    () => syncRoots
      .filter((root) => Boolean(root.remoteWorkspaceId))
      .map((root) => root.localWorkspaceId),
    [syncRoots],
  );
  const selectedTaskRequestWorkspaceId = selectedTaskWorkspaceId ??
    selectedTask?.workspaceId ??
    taskItems.find((taskItem) => taskItem.id === selectedTaskId)?.workspaceId ??
    selectedWorkspaceId;
  const cloudSyncSelectionRef = useRef({
    taskId: selectedTaskId,
    taskWorkspaceId: selectedTaskRequestWorkspaceId,
    viewId: currentViewId,
    workspaceId: selectedWorkspaceId,
  });
  cloudSyncSelectionRef.current = {
    taskId: selectedTaskId,
    taskWorkspaceId: selectedTaskRequestWorkspaceId,
    viewId: currentViewId,
    workspaceId: selectedWorkspaceId,
  };

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
      const session = await loadAuthSession();
      setAuthOptions(session.authOptions);
      setAuthSessions(session.authSessions);
      setConnectionStatus('online');
      setCurrentUser(session.currentUser);
      setIncomingWorkspaceInvitations(session.incomingWorkspaceInvitations);
      setIncomingTaskShares(session.incomingTaskShares);
      setLocalDesktopSessionIsActive(session.localDesktopSessionIsActive);
      setCloudSyncAccount(session.localDesktopSessionIsActive
        ? await getCloudSyncAccount()
        : null);
      setTemporarySessionIsActive(session.temporarySessionIsActive);
      setErrorMessage(null);
    } catch (error) {
      setConnectionStatus('offline');
      setCloudSyncAccount(null);
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
    setSyncRoots(snapshot.syncRoots ?? []);
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
          roots,
        ] = await Promise.all([
          getWorkspace(workspaceRequestOptions),
          listSavedViews(workspaceRequestOptions),
          listProjects(workspaceRequestOptions),
          listArchiveResolutions(workspaceRequestOptions),
          listTaskTemplates(workspaceRequestOptions),
          listWorkspaceMembers(workspaceRequestOptions).catch(() => []),
          listWorkspaceInvitations(workspaceRequestOptions).catch(() => []),
          localDesktopSessionIsActive
            ? listWorkspaceSyncRoots({
                signal: controller.signal,
                workspaceId: null,
              }).catch((error) => {
                if (isAbortError(error)) {
                  throw error;
                }

                return [];
              })
            : Promise.resolve([]),
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
        setSyncRoots(roots);
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
          syncRoots: roots,
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
          setSelectedTaskWorkspaceId(null);
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
      localDesktopSessionIsActive,
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
        void getTaskItem(selectedTaskId, { workspaceId: selectedTaskRequestWorkspaceId })
          .then((taskItem) => {
            setSelectedTaskWorkspaceId(taskItem.workspaceId);
            setSelectedTask(taskItem);
          })
          .catch(() => {
            setSelectedTaskId(null);
            setSelectedTaskWorkspaceId(null);
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
    selectedTaskRequestWorkspaceId,
    selectedWorkspaceId,
    showToast,
    t,
    temporarySessionIsActive,
  ]);

  const performBackgroundCloudSync = useCallback(async (workspaceId: string) => {
    if (
      !localDesktopSessionIsActive ||
      !cloudSyncAccount?.isConnected ||
      cloudSyncInFlightRef.current.has(workspaceId) ||
      document.visibilityState === 'hidden' ||
      !navigator.onLine ||
      (cloudSyncRetryAfterRef.current.get(workspaceId) ?? 0) > Date.now()
    ) {
      return;
    }

    const root = syncRootsRef.current.find(
      (candidate) =>
        candidate.localWorkspaceId === workspaceId &&
        Boolean(candidate.remoteWorkspaceId),
    );
    if (!root) {
      return;
    }

    cloudSyncInFlightRef.current.add(workspaceId);
    try {
      const response = await syncWorkspaceWithCloud(workspaceId, {
        pushLocalChanges: true,
        pullRemoteChanges: true,
      });
      setSyncRoots((currentRoots) => {
        const nextRoots = currentRoots.some((candidate) => candidate.id === response.root.id)
          ? currentRoots.map((candidate) =>
              candidate.id === response.root.id ? response.root : candidate)
          : [...currentRoots, response.root];
        syncRootsRef.current = nextRoots;
        return nextRoots;
      });
      cloudSyncRetryAfterRef.current.delete(workspaceId);

      try {
        setCloudSyncAccount(await getCloudSyncAccount());
      } catch {
        // Sync succeeded; stale account presentation is less harmful than failing the sync.
      }

      const currentSelection = cloudSyncSelectionRef.current;
      if (
        currentSelection.workspaceId === workspaceId &&
        (response.pulled > 0 ||
          response.updatedLocal > 0 ||
          response.conflicts > 0 ||
          response.failed > 0)
      ) {
        await loadWorkspace(currentSelection.viewId, workspaceId, {
          force: true,
          silent: true,
        });
      }

      const taskSelection = cloudSyncSelectionRef.current;
      if (
        taskSelection.taskId &&
        taskSelection.taskWorkspaceId === workspaceId &&
        response.updatedLocal > 0
      ) {
        const taskId = taskSelection.taskId;
        const refreshedTask = await getTaskItem(taskId, { workspaceId });
        const latestSelection = cloudSyncSelectionRef.current;
        if (
          latestSelection.taskId === taskId &&
          latestSelection.taskWorkspaceId === workspaceId
        ) {
          setSelectedTask(refreshedTask);
        }
      }
    } catch (error) {
      const now = Date.now();
      cloudSyncRetryAfterRef.current.set(workspaceId, now + 30000);
      if (now - cloudSyncFailureToastAtRef.current > 60000) {
        cloudSyncFailureToastAtRef.current = now;
        showToast(`${t('cloudSyncPaused')}: ${getErrorMessage(error)}`, 'warning');
      }
    } finally {
      cloudSyncInFlightRef.current.delete(workspaceId);
    }
  }, [
    cloudSyncAccount?.isConnected,
    loadWorkspace,
    localDesktopSessionIsActive,
    showToast,
    t,
  ]);

  useEffect(() => {
    if (!localDesktopSessionIsActive || !cloudSyncAccount?.isConnected) {
      return undefined;
    }

    let intervalTick = 0;
    const syncLinkedWorkspaces = (includeAll: boolean) => {
      if (document.visibilityState === 'hidden' || !navigator.onLine) {
        return;
      }

      const activeWorkspaceId = cloudSyncSelectionRef.current.workspaceId;
      syncRootsRef.current
        .filter((root) =>
          Boolean(root.remoteWorkspaceId) &&
          (includeAll || root.localWorkspaceId === activeWorkspaceId))
        .forEach((root) => void performBackgroundCloudSync(root.localWorkspaceId));
    };
    const resumeSync = () => syncLinkedWorkspaces(false);
    const initialTimer = window.setTimeout(() => syncLinkedWorkspaces(true), 1500);
    const interval = window.setInterval(() => {
      intervalTick += 1;
      syncLinkedWorkspaces(intervalTick % 4 === 0);
    }, 15000);
    window.addEventListener('online', resumeSync);
    document.addEventListener('visibilitychange', resumeSync);

    return () => {
      window.clearTimeout(initialTimer);
      window.clearInterval(interval);
      window.removeEventListener('online', resumeSync);
      document.removeEventListener('visibilitychange', resumeSync);
    };
  }, [
    cloudSyncAccount?.isConnected,
    localDesktopSessionIsActive,
    performBackgroundCloudSync,
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
      workspaceId: selectedTaskRequestWorkspaceId,
      signal: controller.signal,
    })
      .then((taskItem) => {
        if (requestIsStale) {
          return;
        }

        setSelectedTask(taskItem);
        setSelectedTaskWorkspaceId(taskItem.workspaceId);
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
  }, [mode, selectedTaskId, selectedTaskRequestWorkspaceId]);

  const handleSelectSavedView = (viewId: string) => {
    setMode('tasks');
    setCurrentViewId(viewId);
    setSelectedTaskId(null);
    setSelectedTaskWorkspaceId(null);
    updateUrl('tasks', viewId);
    void loadWorkspace(viewId);
  };

  const handleSelectWorkspace = (workspaceId: string) => {
    setMode('tasks');
    setSelectedWorkspaceId(workspaceId);
    setCurrentViewId(null);
    setSelectedTaskId(null);
    setSelectedTaskWorkspaceId(null);
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
      await loadAuth();
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
        setSelectedTaskWorkspaceId(null);
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
        setSelectedTaskWorkspaceId(null);
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
    setAuthSessions([
      {
        id: userState.session.id,
        sessionType: userState.session.sessionType,
        deviceName: userState.session.deviceName,
        createdAt: userState.session.createdAt,
        expiresAt: userState.session.expiresAt,
        lastSeenAt: userState.session.lastSeenAt,
        revokedAt: null,
        isCurrent: true,
      },
    ]);
    const isDesktopLocalSession =
      userState.session.sessionType === 'DesktopLocal' ||
        userState.session.sessionType === 2;
    setLocalDesktopSessionIsActive(isDesktopLocalSession);
    setTemporarySessionIsActive(
      !isDesktopLocalSession &&
        (userState.session.sessionType === 'Guest' ||
          userState.session.sessionType === 5 ||
          isTemporarySession()),
    );
    const workspaceId = userState.workspaces[0]?.id ?? null;
    setSelectedWorkspaceId(workspaceId);
    setSelectedTaskId(null);
    setSelectedTaskWorkspaceId(null);
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
    if (localDesktopSessionIsActive) {
      return;
    }

    try {
      await logoutUser();
      setCurrentUser(null);
      setAuthSessions([]);
      setLocalDesktopSessionIsActive(false);
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
      setSelectedTaskWorkspaceId(null);
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

  const handleRevokeAuthSession = async (sessionId: string) => {
    const session = authSessions.find((candidate) => candidate.id === sessionId);
    if (session?.sessionType === 'DesktopLocal' || session?.sessionType === 2) {
      return;
    }

    try {
      await revokeAuthSession(sessionId);
      const revokedCurrentSession = authSessions.some((session) =>
        session.id === sessionId && session.isCurrent);

      if (revokedCurrentSession) {
        await handleLogout();
        return;
      }

      setAuthSessions((currentSessions) =>
        currentSessions.filter((session) => session.id !== sessionId),
      );
      showToast(t('sessionRevoked'), 'info');
      setErrorMessage(null);
    } catch (error) {
      const message = getErrorMessage(error);
      setErrorMessage(message);
      showToast(message, 'error');
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
    setSelectedTaskWorkspaceId(null);
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
    options: CreateTaskItemOptions = {},
  ) => {
    const targetWorkspaceId = options.workspaceId ?? workspace?.id ?? null;

    if (!targetWorkspaceId) {
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
        workspaceId: targetWorkspaceId,
      });
      setMode('tasks');
      setSelectedTaskId(null);
      setSelectedTaskWorkspaceId(null);
      setSelectedTask(null);
      if (targetWorkspaceId !== selectedWorkspaceId) {
        setSelectedWorkspaceId(targetWorkspaceId);
        setCurrentViewId(null);
        updateUrl('tasks', null);
        await loadWorkspace(null, targetWorkspaceId, { force: true });
      } else {
        setTaskItems((currentItems) => {
          const selectedWorkspaceIsAggregate = workspace
            ? isSystemAllTasksWorkspace(workspace)
            : false;

          if (!selectedWorkspaceIsAggregate && created.workspaceId !== selectedWorkspaceId) {
            return currentItems;
          }

          return [created, ...currentItems];
        });
      }
      if (currentViewId && targetWorkspaceId === selectedWorkspaceId) {
        setViewCounts((counts) => ({
          ...counts,
          [currentViewId]: (counts[currentViewId] ?? 0) + 1,
        }));
      }
      void performBackgroundCloudSync(targetWorkspaceId);
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

  const handleSyncWorkspaceWithCloud = async (
    workspaceId: string,
    requestBody: SyncWorkspaceWithCloudRequest,
  ): Promise<SyncWorkspaceWithCloudResponse> => {
    try {
      const response = await syncWorkspaceWithCloud(workspaceId, requestBody);
      setSyncRoots((currentRoots) => {
        const nextRoots = currentRoots.some((candidate) => candidate.id === response.root.id)
          ? currentRoots.map((candidate) =>
              candidate.id === response.root.id ? response.root : candidate)
          : [...currentRoots, response.root];
        syncRootsRef.current = nextRoots;
        return nextRoots;
      });
      await loadWorkspace(currentViewId, workspaceId, {
        force: true,
        silent: true,
      });
      const hasProblems = response.conflicts > 0 || response.failed > 0;
      showToast(
        hasProblems
          ? `${t('syncComplete')}: ${response.conflicts} ${t('syncConflicts')}, ${response.failed} ${t('syncFailedCount')}.`
          : t('syncComplete'),
        hasProblems ? 'warning' : 'info',
      );

      return response;
    } catch (error) {
      const message = getErrorMessage(error);
      showToast(message, 'error');
      throw error;
    }
  };

  const handleConnectCloudAccount = async (requestBody: ConnectCloudAccountRequest) => {
    try {
      const account = await connectCloudSyncAccount(requestBody);
      setCloudSyncAccount(account);
      showToast(t('cloudAccountConnected'), 'info');
    } catch (error) {
      const message = getErrorMessage(error);
      showToast(message, 'error');
      throw error;
    }
  };

  const handleDisconnectCloudAccount = async () => {
    try {
      await disconnectCloudSyncAccount();
      setCloudSyncAccount(null);
      showToast(t('cloudAccountDisconnected'), 'info');
    } catch (error) {
      const message = getErrorMessage(error);
      showToast(message, 'error');
      throw error;
    }
  };

  const handleUpdateTaskItem = async (requestBody: UpdateTaskItemRequest) => {
    if (!selectedTask) {
      return;
    }

    try {
      const updated = await updateTaskItem(
        selectedTask.id,
        requestBody,
        { workspaceId: selectedTask.workspaceId },
      );
      applyTaskUpdate(updated);
      void performBackgroundCloudSync(updated.workspaceId);
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
      const targetTask = selectedTask?.id === taskItemId ? selectedTask : null;
      const requestOptions = { workspaceId: targetTask?.workspaceId ?? selectedTaskRequestWorkspaceId };
      const created = await createTaskShareLink(taskItemId, requestBody, requestOptions);
      const updated = await getTaskItem(taskItemId, requestOptions);
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
      const targetTask = selectedTask?.id === taskItemId ? selectedTask : null;
      const updated = await revokeTaskShare(
        taskItemId,
        shareId,
        { workspaceId: targetTask?.workspaceId ?? selectedTaskRequestWorkspaceId },
      );
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
      const targetTask = selectedTask?.id === taskItemId ? selectedTask : null;
      const updated = await updateTaskShareRole(
        taskItemId,
        shareId,
        requestBody,
        { workspaceId: targetTask?.workspaceId ?? selectedTaskRequestWorkspaceId },
      );
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
      await Promise.all(taskItemIds.map((taskItemId) => {
        const taskItem = taskItems.find((currentTask) => currentTask.id === taskItemId);
        return updateTaskItem(taskItemId, requestBody, { workspaceId: taskItem?.workspaceId });
      }));
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
      }, {
        workspaceId: selectedTask.workspaceId,
      });
      applyTaskUpdate(updated);
      void performBackgroundCloudSync(updated.workspaceId);
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
      }, {
        workspaceId: selectedTask.workspaceId,
      });
      applyTaskUpdate(updated);
      void performBackgroundCloudSync(updated.workspaceId);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  const handleDeleteTimelineEntry = async (entryId: string) => {
    if (!selectedTask) {
      return;
    }

    try {
      const updated = await deleteTaskTimelineEntry(
        selectedTask.id,
        entryId,
        { workspaceId: selectedTask.workspaceId },
      );
      applyTaskUpdate(updated);
      void performBackgroundCloudSync(updated.workspaceId);
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
    }
  };

  const handleArchiveTaskItem = async (requestBody: ArchiveTaskItemRequest) => {
    if (!selectedTask) {
      return;
    }

    try {
      const archived = await archiveTaskItem(
        selectedTask.id,
        requestBody,
        { workspaceId: selectedTask.workspaceId },
      );
      const archiveViewId = findViewId(savedViews, 'Archive') ?? currentViewId;
      setCurrentViewId(archiveViewId);
      setSelectedTaskId(archived.id);
      setSelectedTaskWorkspaceId(archived.workspaceId);
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
        taskItemIds.map((taskItemId) => {
          const taskItem = taskItems.find((currentTask) => currentTask.id === taskItemId);
          return archiveTaskItem(taskItemId, requestBody, { workspaceId: taskItem?.workspaceId });
        }),
      );
      setSelectedTaskId(null);
      setSelectedTaskWorkspaceId(null);
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
      const reopened = await reopenTaskItem(
        selectedTask.id,
        { note },
        { workspaceId: selectedTask.workspaceId },
      );
      const activeViewId = findViewId(savedViews, 'All Tasks') ??
        findViewId(savedViews, 'Overview') ??
        currentViewId;
      setCurrentViewId(activeViewId);
      setSelectedTaskId(reopened.id);
      setSelectedTaskWorkspaceId(reopened.workspaceId);
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
      setSelectedTaskWorkspaceId(null);
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
      setSelectedTaskWorkspaceId(null);
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

  const handleDeleteOldArchivedTasks = async (
    workspaceId: string,
    olderThanDays: number,
    status: string | null = null,
  ) => {
    try {
      const archivedTasks = await listTaskItems(
        { archive: 'Archived' },
        { workspaceId },
      );
      const cutoff = Date.now() - Math.max(0, olderThanDays) * 24 * 60 * 60 * 1000;
      const taskItemIds = archivedTasks
        .filter((taskItem) =>
          taskItem.archivedAt &&
          new Date(taskItem.archivedAt).getTime() <= cutoff &&
          (!status ||
            taskItem.status?.localeCompare(status, undefined, {
              sensitivity: 'accent',
            }) === 0))
        .map((taskItem) => taskItem.id);

      if (taskItemIds.length === 0) {
        showToast(t('noArchivedTasksToDelete'), 'info');
        return 0;
      }

      await deleteTaskItemsPermanently(
        { taskItemIds },
        { workspaceId },
      );

      if (selectedWorkspaceId === workspaceId) {
        await loadWorkspace(currentViewId, workspaceId, { force: true });
      }

      showToast(t('tasksDeletedPermanently'));
      return taskItemIds.length;
    } catch (error) {
      const message = getErrorMessage(error);
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

  const handleImportTaskTemplate = async (taskItemId: string) => {
    const taskWorkspaceId = selectedTask?.workspaceId ??
      taskItems.find((taskItem) => taskItem.id === taskItemId)?.workspaceId ??
      selectedTaskRequestWorkspaceId;

    try {
      const imported = await importTaskTemplateFromTask(taskItemId, {
        workspaceId: taskWorkspaceId,
      });

      setTemplates((currentTemplates) => {
        const withoutDuplicate = currentTemplates.filter(
          (template) => template.id !== imported.template.id,
        );

        return [...withoutDuplicate, imported.template]
          .sort((first, second) => first.name.localeCompare(second.name));
      });
      setImportedTemplateSourceIds((currentIds) =>
        currentIds.includes(imported.sourceTemplateId)
          ? currentIds
          : [...currentIds, imported.sourceTemplateId]);
      showToast(t('templateImported'));
      setErrorMessage(null);
    } catch (error) {
      const message = getErrorMessage(error);
      setErrorMessage(message);
      showToast(message, 'error');
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
        setSelectedTaskWorkspaceId(null);
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
        localDesktopSessionIsActive={localDesktopSessionIsActive}
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
            localDesktopSessionIsActive={localDesktopSessionIsActive}
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
            cloudSyncAccount={cloudSyncAccount}
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
            onImportTaskTemplate={handleImportTaskTemplate}
            onOpenArchiveDialog={() => setArchiveDialogIsOpen(true)}
            onReopen={handleReopenTaskItem}
            onReopenTaskItems={handleReopenTaskItems}
            onDeleteTaskItemsPermanently={handleDeleteTaskItemsPermanently}
            onRevokeTaskShare={handleRevokeTaskShare}
            onRevokeWorkspaceInvitation={handleRevokeWorkspaceInvitation}
            onRemoveWorkspaceMember={handleRemoveWorkspaceMember}
            onSelectTaskItem={(id, workspaceId) => {
              setSelectedTaskId(id);
              setSelectedTaskWorkspaceId(workspaceId);
            }}
            onCloseTaskItem={() => {
              setSelectedTaskId(null);
              setSelectedTaskWorkspaceId(null);
              setSelectedTask(null);
            }}
            onUpdateFieldValues={handleUpdateFieldValues}
            onUpdateTaskShareRole={handleUpdateTaskShareRole}
            onUpdateTaskItems={handleUpdateTaskItems}
            onUpdateTaskItem={handleUpdateTaskItem}
            onUpdateTimelineEntry={handleUpdateTimelineEntry}
            onUpdateProject={handleUpdateProject}
            onSyncWorkspaceWithCloud={handleSyncWorkspaceWithCloud}
            onUpdateWorkspace={handleUpdateWorkspace}
            onUpdateWorkspaceMemberRole={handleUpdateWorkspaceMemberRole}
            onShowToast={showToast}
            projects={projects}
            localDesktopSessionIsActive={localDesktopSessionIsActive}
            selectedTask={selectedTask}
            selectedTaskId={selectedTaskId}
            statusOptions={statusOptions}
            syncRoot={syncRoots.find((root) => root.localWorkspaceId === workspace?.id) ?? null}
            taskItems={taskItems}
            templates={templates}
            importedTemplateSourceIds={importedTemplateSourceIds}
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
          cleanupCloudLinkedWorkspaceIds={cloudLinkedWorkspaceIds}
          cleanupPreferredWorkspaceId={workspace?.id ?? null}
          cleanupWorkspaces={cleanupWorkspaces}
          configuredStatuses={configuredStatuses}
          language={language}
          onChangeLanguage={setLanguage}
          onCreateArchiveResolution={handleCreateArchiveResolution}
          onDeleteArchiveResolution={handleDeleteArchiveResolution}
          onDeleteOldArchivedTasks={handleDeleteOldArchivedTasks}
          onDeleteWorkspace={handleDeleteWorkspace}
          onSaveStatusOptions={handleSaveStatusOptions}
          onUpdateArchiveResolution={handleUpdateArchiveResolution}
          onClose={() => setSettingsIsOpen(false)}
          t={t}
        />
      ) : null}
      {accountIsOpen ? (
        <AccountPanel
          authSessions={authSessions}
          authOptions={authOptions}
          cloudSyncAccount={cloudSyncAccount}
          currentUser={currentUser}
          incomingTaskShares={incomingTaskShares}
          incomingWorkspaceInvitations={incomingWorkspaceInvitations}
          isLoadingAuth={isLoadingAuth}
          onAcceptIncomingWorkspaceInvitation={handleAcceptIncomingWorkspaceInvitation}
          onClose={() => setAccountIsOpen(false)}
          onDeclineIncomingWorkspaceInvitation={handleDeclineIncomingWorkspaceInvitation}
          onConnectCloudAccount={handleConnectCloudAccount}
          onDisconnectCloudAccount={handleDisconnectCloudAccount}
          onDevelopmentLogin={handleDevelopmentLogin}
          onGuestLogin={handleGuestLogin}
          onLeaveTaskShare={handleLeaveTaskShare}
          onLogin={handleLogin}
          onLogout={handleLogout}
          onRegister={handleRegister}
          onRevokeAuthSession={handleRevokeAuthSession}
          localDesktopSessionIsActive={localDesktopSessionIsActive}
          temporarySessionIsActive={temporarySessionIsActive}
          t={t}
        />
      ) : null}
      <ToastStack onDismiss={dismissToast} toasts={toasts} />
    </main>
  );
}



export default App;
