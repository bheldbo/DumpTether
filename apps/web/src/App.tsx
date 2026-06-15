import {
  type CSSProperties,
  type DragEvent,
  FormEvent,
  type KeyboardEvent,
  type MouseEvent,
  type PointerEvent as ReactPointerEvent,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import { Icon, type IconName } from './components/Icon';
import { ColorOptionPicker } from './components/ColorOptionPicker';
import { ModalFrame } from './components/ModalFrame';
import { TaskFilterBar } from './components/TaskFilterBar';
import { ToastStack } from './components/ToastStack';
import {
  defaultAuthOptions,
  fieldTypes,
  maxSidebarWidth,
  minSidebarWidth,
  sidebarWidthStorageKey,
  statusOptionsStorageKey,
  workspaceStorageKey,
  type ConnectionStatus,
  type EditableTemplateField,
  type SettingsSectionKey,
  type ToastMessage,
  type ToastTone,
  type WorkspaceMode,
  languageStorageKey,
} from './appTypes';
import {
  buildShareUrl,
  clamp,
  copyTextToClipboard,
  findViewId,
  formatDateTime,
  formatFullDate,
  formatOAuthProvider,
  formatRelativeDate,
  formatSavedViewName,
  formatSortField,
  formatTaskShareRole,
  formatWorkspaceRole,
  formatWorkspaceName,
  getErrorMessage,
  getInitialLanguage,
  getInitialMode,
  getInitialViewId,
  getInitialWorkspaceId,
  getViewIcon,
  isAbortError,
  isOwnerRole,
  isReadOnlyRole,
  isReadOnlyTaskShareRole,
  isTaskShareWorkspace,
  isTextEditingTarget,
  pickSavedViewId,
  readStoredStringList,
  toDateInputValue,
  updateUrl,
} from './appUtils';
import {
  addTaskTimelineEntry,
  acceptShareLink,
  acceptIncomingWorkspaceInvitation,
  ApiError,
  archiveTaskItem,
  beginOAuthLogin,
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
import { FieldEditorList, FieldValueList } from './fieldRenderers';
import { toFieldValueMap } from './fieldValues';
import {
  ArchiveDialog,
  PermanentDeleteDialog,
  ReopenDialog,
} from './features/task-detail/TaskDialogs';
import { TimelinePanel } from './features/timeline/TimelinePanel';
import { startLiveUpdates, type LiveUpdateMessage } from './liveUpdates';
import { type Language, type Translate, translate } from './localization';
import {
  FIELD_LAYOUT_MAX_COLUMNS,
  getEditableTemplateFieldGridStyle,
  getTemplateLayoutGridStyle,
  normalizeTemplateLayoutFields,
} from './templateLayout';
import {
  applyTaskWallFilters,
  buildTaskFilterOptions,
  colorChoices,
  emptyTaskWallFilters,
  getContextChipStyle,
  getFollowUpTone,
  getPrimaryProjectIdForCategories,
  getProjectsForTaskCategories,
  getSidebarStyle,
  getTaskBadges,
  getTaskCardStyle,
  getTaskColors,
  getTaskState,
  getWorkspaceHeaderStyle,
  isHexColor,
  joinTaskCategories,
  mergeColorOptions,
  splitTaskCategories,
  taskWallFiltersAreActive,
  type TaskWallFilters,
  uniqueSorted,
} from './taskUtils';
import {
  clampInteger,
  renumberTemplateFields,
  splitOptions,
  toEditableTemplateField,
  withDefaultFieldValues,
} from './templateFieldUtils';
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
  FieldDefinitionScope,
  FieldDefinitionType,
  FieldValueMap,
  LoginUserRequest,
  ProjectResponse,
  RegisterUserRequest,
  RegisterUserResponse,
  SavedViewResponse,
  TaskItemDetailResponse,
  TaskItemShareRole,
  TaskItemShareResponse,
  TaskItemSummaryResponse,
  TaskShareInboxResponse,
  TaskShareLinkResponse,
  TaskTemplateDetailResponse,
  WorkspaceInvitationInboxResponse,
  WorkspaceInvitationResponse,
  WorkspaceMembershipRole,
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
          selectedViewId
            ? listTaskItems({ viewId: selectedViewId }, workspaceRequestOptions)
            : Promise.resolve([]),
          listTaskViewCounts(
            views.map((view) => view.id),
            workspaceRequestOptions,
          ),
          listTaskItems({ archive: 'All' }, workspaceRequestOptions),
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
  ): Promise<TaskTemplateDetailResponse | null> => {
    try {
      const savedTemplate = id
        ? await updateTaskTemplate(id, { name, fields })
        : await createTaskTemplate({ name, fields });

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

function Sidebar({
  accountNotificationCount,
  connectionStatus,
  counts,
  currentUser,
  currentViewId,
  lastPingedAt,
  language,
  mode,
  onCreateWorkspace,
  onDeleteWorkspace,
  onLeaveWorkspaceAccess,
  onOpenAccount,
  onOpenSettings,
  onOpenTemplates,
  onRefresh,
  onResizeStart,
  onSelectWorkspace,
  onSelectView,
  onToggleSidebar,
  onUpdateWorkspace,
  savedViews,
  sidebarIsCollapsed,
  t,
  temporarySessionIsActive,
  templateCount,
  workspace,
  workspaces,
}: {
  accountNotificationCount: number;
  connectionStatus: ConnectionStatus;
  counts: Record<string, number>;
  currentUser: CurrentUserResponse | null;
  currentViewId: string | null;
  lastPingedAt: string | null;
  language: Language;
  mode: WorkspaceMode;
  onCreateWorkspace: (name: string) => Promise<void>;
  onDeleteWorkspace: (workspaceId: string) => Promise<void>;
  onLeaveWorkspaceAccess: (workspaceId: string) => Promise<void>;
  onOpenAccount: () => void;
  onOpenSettings: () => void;
  onOpenTemplates: () => void;
  onRefresh: () => void;
  onResizeStart: (event: MouseEvent<HTMLButtonElement>) => void;
  onSelectWorkspace: (workspaceId: string) => void;
  onSelectView: (viewId: string) => void;
  onToggleSidebar: () => void;
  onUpdateWorkspace: (
    workspaceId: string,
    requestBody: UpdateWorkspaceRequest,
  ) => Promise<void>;
  savedViews: SavedViewResponse[];
  sidebarIsCollapsed: boolean;
  t: Translate;
  temporarySessionIsActive: boolean;
  templateCount: number;
  workspace: WorkspaceResponse | null;
  workspaces: WorkspaceResponse[];
}) {
  const [workspaceDraft, setWorkspaceDraft] = useState('');
  const [workspaceCreateIsOpen, setWorkspaceCreateIsOpen] = useState(false);
  const [editingWorkspaceId, setEditingWorkspaceId] = useState<string | null>(null);
  const [editingWorkspaceName, setEditingWorkspaceName] = useState('');
  const [pendingDeleteWorkspace, setPendingDeleteWorkspace] =
    useState<WorkspaceResponse | null>(null);
  const [pendingWorkspaceLeaveId, setPendingWorkspaceLeaveId] = useState<string | null>(null);
  const [workspaceIsSubmitting, setWorkspaceIsSubmitting] = useState(false);
  const workspaceCreateFormRef = useRef<HTMLFormElement>(null);
  const workspaceInputRef = useRef<HTMLInputElement>(null);
  const workspaceCreateToggleRef = useRef<HTMLButtonElement>(null);
  const workspaceMembershipsById = useMemo(
    () => new Map(currentUser?.workspaces.map((workspaceItem) => [workspaceItem.id, workspaceItem]) ?? []),
    [currentUser],
  );
  const visibleSavedViews = useMemo(
    () => savedViews.filter((view) => ['all tasks', 'overview', 'archive'].includes(view.name.toLowerCase())),
    [savedViews],
  );

  useEffect(() => {
    if (workspaceCreateIsOpen) {
      workspaceInputRef.current?.focus();
    }
  }, [workspaceCreateIsOpen]);

  useEffect(() => {
    if (!workspaceCreateIsOpen) {
      return undefined;
    }

    const closeWorkspaceCreate = () => {
      setWorkspaceCreateIsOpen(false);
      setWorkspaceDraft('');
    };
    const handlePointerDown = (event: PointerEvent) => {
      const target = event.target;

      if (!(target instanceof Node)) {
        return;
      }

      if (
        workspaceCreateFormRef.current?.contains(target) ||
        workspaceCreateToggleRef.current?.contains(target)
      ) {
        return;
      }

      closeWorkspaceCreate();
    };
    const handleKeyDown = (event: globalThis.KeyboardEvent) => {
      if (event.key === 'Escape') {
        closeWorkspaceCreate();
      }
    };

    window.addEventListener('pointerdown', handlePointerDown);
    window.addEventListener('keydown', handleKeyDown);

    return () => {
      window.removeEventListener('pointerdown', handlePointerDown);
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [workspaceCreateIsOpen]);

  const submitWorkspace = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const trimmedName = workspaceDraft.trim();

    if (!trimmedName) {
      return;
    }

    setWorkspaceIsSubmitting(true);
    try {
      await onCreateWorkspace(trimmedName);
      setWorkspaceDraft('');
      setWorkspaceCreateIsOpen(false);
    } finally {
      setWorkspaceIsSubmitting(false);
    }
  };

  const startWorkspaceEdit = (workspaceItem: WorkspaceResponse) => {
    setEditingWorkspaceId(workspaceItem.id);
    setEditingWorkspaceName(workspaceItem.name);
    setPendingWorkspaceLeaveId(null);
  };

  const cancelWorkspaceEdit = () => {
    setEditingWorkspaceId(null);
    setEditingWorkspaceName('');
  };

  const submitWorkspaceEdit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const trimmedName = editingWorkspaceName.trim();
    if (!editingWorkspaceId || !trimmedName) {
      return;
    }

    await onUpdateWorkspace(editingWorkspaceId, { name: trimmedName });
    cancelWorkspaceEdit();
  };

  return (
    <>
      <aside
        className="sidebar"
        aria-label="DumpTether navigation"
        style={getSidebarStyle(workspace?.color ?? null)}
      >
      <div className="brand">
        <button
          className="brand-mark"
          onClick={sidebarIsCollapsed ? onToggleSidebar : undefined}
          title={sidebarIsCollapsed ? t('expandSidebar') : undefined}
          type="button"
        >
          DT
        </button>
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
      {!sidebarIsCollapsed ? (
        <button
          aria-label="Resize sidebar"
          className="sidebar-resizer"
          onMouseDown={onResizeStart}
          type="button"
        />
      ) : null}

      <div className="sidebar-section-label">
        <span>{t('workspaces')}</span>
        <button
          className="tiny-icon-button"
          onClick={() => setWorkspaceCreateIsOpen((isOpen) => !isOpen)}
          ref={workspaceCreateToggleRef}
          title={t('newWorkspace')}
          type="button"
        >
          <Icon name="plus" />
        </button>
      </div>

      <nav className="view-nav workspace-nav" aria-label={t('workspaces')}>
        {workspaces.map((candidate) => {
          const isSharedOnly = isTaskShareWorkspace(candidate);
          const membership = workspaceMembershipsById.get(candidate.id);
          const isOwner = Boolean(membership && isOwnerRole(membership.role));
          const isSharedMembership = Boolean(membership && !isOwnerRole(membership.role));
          const canDelete = Boolean(membership && isOwnerRole(membership.role) && !isSharedOnly);
          const canEdit = canDelete;
          const canLeave = isSharedOnly ||
            isSharedMembership;
          const isEditing = editingWorkspaceId === candidate.id;
          const leaveIsPending = pendingWorkspaceLeaveId === candidate.id;
          const isSharedAccess = isSharedOnly || isSharedMembership;
          const ownerSharedSignalIsVisible = isOwner &&
            !isSharedAccess &&
            ((candidate.memberCount ?? 1) > 1 ||
              (candidate.pendingInvitationCount ?? 0) > 0);

          return (
            <div
              className="workspace-nav-row"
              key={candidate.id}
            >
              {isEditing ? (
                <form className="workspace-row-editor" onSubmit={(event) => void submitWorkspaceEdit(event)}>
                  <span
                    className="workspace-color-dot"
                    style={{ backgroundColor: candidate.color ?? '#184c48' }}
                  />
                  <input
                    aria-label={t('editBoard')}
                    autoFocus
                    onChange={(event) => setEditingWorkspaceName(event.target.value)}
                    onKeyDown={(event) => {
                      if (event.key === 'Escape') {
                        cancelWorkspaceEdit();
                      }
                    }}
                    type="text"
                    value={editingWorkspaceName}
                  />
                  <button
                    className="tiny-icon-button"
                    disabled={!editingWorkspaceName.trim()}
                    title={t('saved')}
                    type="submit"
                  >
                    <Icon name="check" />
                  </button>
                  <button
                    className="tiny-icon-button"
                    onClick={cancelWorkspaceEdit}
                    title={t('cancel')}
                    type="button"
                  >
                    <Icon name="close" />
                  </button>
                  {canDelete ? (
                    <button
                      className="tiny-icon-button danger-icon-button"
                      onClick={() => setPendingDeleteWorkspace(candidate)}
                      title={t('deleteBoard')}
                      type="button"
                    >
                      <Icon name="trash" />
                    </button>
                  ) : null}
                </form>
              ) : (
                <button
                  aria-current={workspace?.id === candidate.id ? 'page' : undefined}
                  className={`nav-item workspace-nav-item${isSharedAccess ? ' is-shared-access' : ''}`}
                  onClick={() => onSelectWorkspace(candidate.id)}
                  title={isSharedAccess
                    ? `${formatWorkspaceName(candidate.name, t)} - ${t('sharedWorkspace')}`
                    : formatWorkspaceName(candidate.name, t)}
                  type="button"
                >
                  <span
                    className="workspace-color-dot"
                    style={{ backgroundColor: candidate.color ?? '#184c48' }}
                  />
                  <span className="nav-label">{formatWorkspaceName(candidate.name, t)}</span>
                  {ownerSharedSignalIsVisible ? (
                    <span className="owner-workspace-badge" title={t('roleOwner')}>
                      <Icon name="crown" />
                    </span>
                  ) : null}
                  {isSharedAccess ? (
                    <span className="shared-workspace-badge" title={t('sharedWorkspace')}>
                      <Icon name={isSharedOnly ? 'users' : 'user'} />
                      {isSharedOnly ? candidate.sharedTaskCount ?? 0 : null}
                    </span>
                  ) : null}
                </button>
              )}
              <span className="workspace-row-actions">
                {canEdit && !isEditing ? (
                  <button
                    className="tiny-icon-button workspace-row-action"
                    onClick={() => startWorkspaceEdit(candidate)}
                    title={t('editBoard')}
                    type="button"
                  >
                    <Icon name="edit" />
                  </button>
                ) : null}
                {canLeave ? (
                  leaveIsPending ? (
                    <span className="workspace-row-confirm">
                      <button
                        className="tiny-icon-button"
                        onClick={() => void onLeaveWorkspaceAccess(candidate.id)}
                        title={t('leaveBoard')}
                        type="button"
                      >
                        <Icon name="check" />
                      </button>
                      <button
                        className="tiny-icon-button"
                        onClick={() => setPendingWorkspaceLeaveId(null)}
                        title={t('cancel')}
                        type="button"
                      >
                        <Icon name="close" />
                      </button>
                    </span>
                  ) : (
                    <button
                      className="tiny-icon-button workspace-row-action"
                      onClick={() => setPendingWorkspaceLeaveId(candidate.id)}
                      title={t('leaveBoard')}
                      type="button"
                    >
                      <Icon name="logout" />
                    </button>
                  )
                ) : null}
              </span>
            </div>
          );
        })}
        {workspaceCreateIsOpen ? (
          <form
            className="sidebar-inline-form"
            onSubmit={submitWorkspace}
            ref={workspaceCreateFormRef}
          >
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
            <button
              className="icon-button"
              onClick={() => {
                setWorkspaceDraft('');
                setWorkspaceCreateIsOpen(false);
              }}
              title={t('cancel')}
              type="button"
            >
              <Icon name="close" />
            </button>
          </form>
        ) : null}
      </nav>

      <div className="sidebar-separator" />

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

      <div className="sidebar-separator sidebar-separator-actions" />

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
        <button className="nav-item" onClick={onOpenAccount} type="button">
          <Icon name="user" />
          <span className="nav-label">{t('account')}</span>
          {accountNotificationCount > 0 ? (
            <span className="nav-count">{accountNotificationCount}</span>
          ) : temporarySessionIsActive ? (
            <span className="nav-count">{t('guestModeShort')}</span>
          ) : null}
        </button>
        <button className="refresh-button" onClick={onRefresh} type="button">
          <Icon name="refresh" />
          <span className="nav-label">{t('refresh')}</span>
        </button>
        {temporarySessionIsActive ? (
          <button className="nav-item guest-warning-link" onClick={onOpenAccount} type="button">
            <Icon name="waiting" />
            <span className="nav-label">{t('guestModeShort')}</span>
          </button>
        ) : null}
        <div className="sidebar-footer">
          <span
            className="connection-indicator"
            data-state={connectionStatus}
            title={`${connectionStatus === 'online' ? t('backendOnline') : t('backendOffline')}${
              lastPingedAt ? ` · ${t('lastPinged')}: ${formatDateTime(lastPingedAt)}` : ''
            }`}
          >
            <span />
            <strong>{connectionStatus === 'online' ? t('online') : t('offline')}</strong>
            {lastPingedAt ? (
              <small>{formatRelativeDate(lastPingedAt)}</small>
            ) : null}
          </span>
          <a href="https://github.com/bheldbo/DumpTether" rel="noreferrer" target="_blank">
            GitHub
          </a>
          <span>© 2026</span>
        </div>
      </div>
      </aside>
      {pendingDeleteWorkspace ? (
        <DeleteWorkspaceDialog
          onClose={() => setPendingDeleteWorkspace(null)}
          onDelete={async () => {
            await onDeleteWorkspace(pendingDeleteWorkspace.id);
            setPendingDeleteWorkspace(null);
          }}
          t={t}
          workspace={pendingDeleteWorkspace}
        />
      ) : null}
    </>
  );
}

function DeleteWorkspaceDialog({
  onClose,
  onDelete,
  t,
  workspace,
}: {
  onClose: () => void;
  onDelete: () => Promise<void>;
  t: Translate;
  workspace: WorkspaceResponse;
}) {
  const [isDeleting, setIsDeleting] = useState(false);

  return (
    <ModalFrame onClose={onClose}>
      <section
        aria-labelledby="delete-workspace-title"
        aria-modal="true"
        className="delete-workspace-dialog"
        role="dialog"
      >
        <div className="dialog-header">
          <div>
            <p className="detail-kicker">{t('deleteBoard')}</p>
            <h2 id="delete-workspace-title">{workspace.name}</h2>
          </div>
          <button className="icon-button" disabled={isDeleting} onClick={onClose} type="button">
            <Icon name="close" />
            <span className="sr-only">{t('close')}</span>
          </button>
        </div>
        <p>{t('deleteBoardConfirmBody')}</p>
        <div className="dialog-actions">
          <button className="ghost-button" disabled={isDeleting} onClick={onClose} type="button">
            {t('cancel')}
          </button>
          <button
            className="danger-action"
            disabled={isDeleting}
            onClick={async () => {
              setIsDeleting(true);
              try {
                await onDelete();
              } finally {
                setIsDeleting(false);
              }
            }}
            type="button"
          >
            <Icon name="trash" />
            {t('deleteBoardNow')}
          </button>
        </div>
      </section>
    </ModalFrame>
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

  return (
    <section
      className="task-board"
      aria-labelledby="task-board-title"
      data-focus-mode={focusModeIsEnabled}
    >
      {!focusModeIsEnabled ? (
        <WorkspaceHeader
          currentView={currentView}
          onCreateProject={onCreateProject}
          onDeleteProject={onDeleteProject}
          onSelectProjectFilter={(projectId) => setFilters((currentFilters) => ({
            ...currentFilters,
            category: '',
            projectId,
          }))}
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
          selectedProjectId={filters.projectId}
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
        <p className="board-refreshing" role="status">
          {t('updatingTasks')}
        </p>
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
            selectedProjectId={filters.projectId}
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
                  {taskCategoryNames.length > 0 && !filters.projectId ? (
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

function WorkspaceHeader({
  canManageWorkspaceMetadata,
  canManageSharing,
  colorOptions,
  currentView,
  invitations,
  members,
  onCreateProject,
  onCreateWorkspaceInvitation,
  onDeleteProject,
  onRemoveWorkspaceMember,
  onRevokeWorkspaceInvitation,
  onSelectProjectFilter,
  onUpdateProject,
  onUpdateWorkspace,
  onUpdateWorkspaceMemberRole,
  projects,
  selectedProjectId,
  t,
  workspace,
}: {
  canManageWorkspaceMetadata: boolean;
  canManageSharing: boolean;
  colorOptions: string[];
  currentView: SavedViewResponse | null;
  invitations: WorkspaceInvitationResponse[];
  members: WorkspaceMemberResponse[];
  onCreateProject: (name: string, color?: string | null) => Promise<void>;
  onCreateWorkspaceInvitation: (
    requestBody: CreateWorkspaceInvitationRequest,
  ) => Promise<WorkspaceInvitationResponse>;
  onDeleteProject: (projectId: string) => Promise<void>;
  onRemoveWorkspaceMember: (userId: string) => Promise<void>;
  onRevokeWorkspaceInvitation: (id: string) => Promise<void>;
  onSelectProjectFilter: (projectId: string) => void;
  onUpdateProject: (id: string, requestBody: UpdateProjectRequest) => Promise<void>;
  onUpdateWorkspace: (requestBody: UpdateWorkspaceRequest) => Promise<void>;
  onUpdateWorkspaceMemberRole: (
    userId: string,
    requestBody: UpdateWorkspaceMemberRequest,
  ) => Promise<WorkspaceMemberResponse>;
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
  const [newProjectIsOpen, setNewProjectIsOpen] = useState(false);
  const [newProjectName, setNewProjectName] = useState('');
  const [newProjectColor, setNewProjectColor] = useState('');
  const [projectIsSubmitting, setProjectIsSubmitting] = useState(false);
  const [inviteIsOpen, setInviteIsOpen] = useState(false);
  const [focusedMemberId, setFocusedMemberId] = useState<string | null>(null);
  const [pendingRemoveMemberId, setPendingRemoveMemberId] = useState<string | null>(null);
  const [pendingDeleteProject, setPendingDeleteProject] = useState<ProjectResponse | null>(null);
  const pendingInvitations = invitations.filter(
    (invitation) => !invitation.acceptedAt && !invitation.revokedAt,
  );

  useEffect(() => {
    setWorkspaceName(workspace?.name ?? '');
    setWorkspaceColor(workspace?.color ?? '');
    setWorkspaceIsEditing(false);
  }, [workspace]);

  useEffect(() => {
    if (!canManageWorkspaceMetadata) {
      setWorkspaceIsEditing(false);
      setEditingProjectId(null);
      setNewProjectIsOpen(false);
      setPendingDeleteProject(null);
    }
  }, [canManageWorkspaceMetadata]);

  const startProjectEditing = (project: ProjectResponse) => {
    setEditingProjectId(project.id);
    setProjectName(project.name);
    setProjectColor(project.color ?? '');
    setPendingDeleteProject(null);
  };

  const cancelProjectEditing = () => {
    setEditingProjectId(null);
    setProjectName('');
    setProjectColor('');
    setPendingDeleteProject(null);
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

  const createProjectFromInlineForm = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const trimmedName = newProjectName.trim();
    if (!trimmedName) {
      return;
    }

    setProjectIsSubmitting(true);
    try {
      await onCreateProject(trimmedName, newProjectColor);
      setNewProjectName('');
      setNewProjectColor('');
      setNewProjectIsOpen(false);
    } finally {
      setProjectIsSubmitting(false);
    }
  };

  return (
    <>
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
              {canManageWorkspaceMetadata ? (
                <button
                  className="heading-edit-trigger"
                  onClick={() => setWorkspaceIsEditing(true)}
                  title={t('editBoard')}
                  type="button"
                >
                  <h1 id="task-board-title">
                    {workspace ? formatWorkspaceName(workspace.name, t) : 'DumpTether'}
                  </h1>
                </button>
              ) : (
                <h1 id="task-board-title">
                  {workspace ? formatWorkspaceName(workspace.name, t) : 'DumpTether'}
                </h1>
              )}
              {canManageWorkspaceMetadata ? (
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
              ) : null}
            </>
          )}
        </div>
        <div className="member-chip-strip" aria-label={t('members')}>
          {members.slice(0, 3).map((member) => (
            <WorkspaceMemberChip
              isConfirming={pendingRemoveMemberId === member.userId}
              key={member.userId}
              member={member}
              onCancelRemove={() => setPendingRemoveMemberId(null)}
              onConfirmRemove={async () => {
                await onRemoveWorkspaceMember(member.userId);
                setPendingRemoveMemberId(null);
              }}
              onOpenManage={() => {
                if (canManageSharing && !isOwnerRole(member.role)) {
                  setFocusedMemberId(member.userId);
                  setInviteIsOpen(true);
                }
              }}
              onRequestRemove={() => setPendingRemoveMemberId(member.userId)}
              t={t}
            />
          ))}
          {members.length > 3 ? (
            <span className="member-chip">+{members.length - 3}</span>
          ) : null}
          {canManageSharing && pendingInvitations.length > 0 ? (
            pendingInvitations.slice(0, 2).map((invitation) => (
              <PendingInvitationChip
                invitation={invitation}
                key={invitation.id}
                onRevoke={() => onRevokeWorkspaceInvitation(invitation.id)}
                t={t}
              />
            ))
          ) : null}
          {canManageSharing && pendingInvitations.length > 2 ? (
            <span className="member-chip member-chip-muted">
              +{pendingInvitations.length - 2} {t('pendingInvites')}
            </span>
          ) : null}
          {canManageSharing ? (
            <button
              className="tiny-icon-button"
              onClick={() => {
                setInviteIsOpen((isOpen) => !isOpen);
              }}
              title={t('inviteMember')}
              type="button"
            >
              <Icon name="plus" />
            </button>
          ) : null}
        </div>
        {inviteIsOpen ? (
          <ShareDialog
            existingTaskShares={[]}
            onClose={() => setInviteIsOpen(false)}
            onCreate={async (email, role) => {
              const created = await onCreateWorkspaceInvitation({
                email,
                role: role as WorkspaceMembershipRole,
              });

              return {
                expiresAt: created.expiresAt,
                token: created.token ?? '',
              };
            }}
            onRemoveWorkspaceMember={onRemoveWorkspaceMember}
            onRevokeTaskShare={undefined}
            onRevokeWorkspaceInvitation={onRevokeWorkspaceInvitation}
            onUpdateWorkspaceMemberRole={onUpdateWorkspaceMemberRole}
            workspaceMembers={members}
            pendingInvitations={pendingInvitations}
            roleMode="workspace"
            t={t}
            title={workspace ? formatWorkspaceName(workspace.name, t) : t('workspaces')}
            focusedWorkspaceMemberId={focusedMemberId}
          />
        ) : null}
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
                <button
                  className="tiny-icon-button danger-icon-button"
                  onClick={() => setPendingDeleteProject(project)}
                  title={t('deleteProjectTag')}
                  type="button"
                >
                  <Icon name="trash" />
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
                {canManageWorkspaceMetadata ? (
                  <button
                    className="tiny-icon-button project-tag-edit"
                    onClick={() => startProjectEditing(project)}
                    title={t('editProject')}
                    type="button"
                  >
                    <Icon name="edit" />
                  </button>
                ) : null}
              </span>
            )
          ))}
          {canManageWorkspaceMetadata && newProjectIsOpen ? (
            <form
              className="project-tag-editor"
              onSubmit={(event) => void createProjectFromInlineForm(event)}
            >
              <input
                aria-label={t('newProjectTag')}
                autoFocus
                onChange={(event) => setNewProjectName(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === 'Escape') {
                    setNewProjectName('');
                    setNewProjectColor('');
                    setNewProjectIsOpen(false);
                  }
                }}
                placeholder={t('newProjectTag')}
                type="text"
                value={newProjectName}
              />
              <ColorPickerPopover
                color={newProjectColor}
                colorOptions={colorOptions}
                label={t('color')}
                onChange={setNewProjectColor}
                t={t}
              />
              <button
                className="tiny-icon-button"
                disabled={!newProjectName.trim() || projectIsSubmitting}
                title={t('saved')}
                type="submit"
              >
                <Icon name="check" />
              </button>
              <button
                className="tiny-icon-button"
                onClick={() => {
                  setNewProjectName('');
                  setNewProjectColor('');
                  setNewProjectIsOpen(false);
                }}
                title={t('cancel')}
                type="button"
              >
                <Icon name="close" />
              </button>
            </form>
          ) : canManageWorkspaceMetadata ? (
            <button
              className="project-tag project-tag-add"
              onClick={() => setNewProjectIsOpen(true)}
              title={t('newProjectTag')}
              type="button"
            >
              <Icon name="plus" />
              <span>{t('newProjectTag')}</span>
            </button>
          ) : null}
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
      {pendingDeleteProject ? (
        <DeleteProjectDialog
          onClose={() => setPendingDeleteProject(null)}
          onDelete={async () => {
            await onDeleteProject(pendingDeleteProject.id);
            if (selectedProjectId === pendingDeleteProject.id) {
              onSelectProjectFilter('');
            }
            cancelProjectEditing();
            setPendingDeleteProject(null);
          }}
          project={pendingDeleteProject}
          t={t}
        />
      ) : null}
    </>
  );
}

function DeleteProjectDialog({
  onClose,
  onDelete,
  project,
  t,
}: {
  onClose: () => void;
  onDelete: () => Promise<void>;
  project: ProjectResponse;
  t: Translate;
}) {
  const [isDeleting, setIsDeleting] = useState(false);

  return (
    <ModalFrame onClose={onClose}>
      <section
        aria-labelledby="delete-project-title"
        aria-modal="true"
        className="delete-workspace-dialog"
        role="dialog"
      >
        <div className="dialog-header">
          <div>
            <p className="detail-kicker">{t('deleteProjectTag')}</p>
            <h2 id="delete-project-title">{project.name}</h2>
          </div>
          <button className="icon-button" disabled={isDeleting} onClick={onClose} type="button">
            <Icon name="close" />
            <span className="sr-only">{t('close')}</span>
          </button>
        </div>
        <p>{t('deleteCategoryWarning')}</p>
        <div className="dialog-actions">
          <button className="ghost-button" disabled={isDeleting} onClick={onClose} type="button">
            {t('cancel')}
          </button>
          <button
            className="danger-action"
            disabled={isDeleting}
            onClick={async () => {
              setIsDeleting(true);
              try {
                await onDelete();
              } finally {
                setIsDeleting(false);
              }
            }}
            type="button"
          >
            <Icon name="trash" />
            {t('deleteCategoryNow')}
          </button>
        </div>
      </section>
    </ModalFrame>
  );
}

function PendingInvitationChip({
  invitation,
  onRevoke,
  t,
}: {
  invitation: WorkspaceInvitationResponse;
  onRevoke: () => Promise<void>;
  t: Translate;
}) {
  return (
    <span className="pending-invite-chip pending-invite-chip-inline" title={invitation.email}>
      <Icon name="mail" />
      <span>{invitation.email}</span>
      <small>{t('pendingInvites')}</small>
      <button
        className="tiny-icon-button"
        onClick={() => void onRevoke()}
        title={t('revokeInvite')}
        type="button"
      >
        <Icon name="close" />
      </button>
    </span>
  );
}

function WorkspaceMemberChip({
  isConfirming,
  member,
  onCancelRemove,
  onConfirmRemove,
  onOpenManage,
  onRequestRemove,
  t,
}: {
  isConfirming: boolean;
  member: WorkspaceMemberResponse;
  onCancelRemove: () => void;
  onConfirmRemove: () => Promise<void>;
  onOpenManage: () => void;
  onRequestRemove: () => void;
  t: Translate;
}) {
  const canRemove = !isOwnerRole(member.role);
  const isOwner = isOwnerRole(member.role);

  return (
    <span
      className={`member-chip member-chip-manageable${isOwner ? ' member-chip-owner' : ''}`}
      data-confirming={isConfirming}
      onClick={(event) => {
        if (event.target instanceof HTMLElement && event.target.closest('.member-chip-remove, .member-chip-confirm')) {
          return;
        }

        onOpenManage();
      }}
      onKeyDown={(event) => {
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault();
          onOpenManage();
        }
      }}
      role={canRemove ? 'button' : undefined}
      tabIndex={canRemove ? 0 : undefined}
      title={isOwner ? `${member.email} - ${t('roleOwner')}` : member.email}
    >
      <Icon name={isOwner ? 'crown' : 'user'} />
      <span>{member.displayName || member.email}</span>
      {canRemove ? (
        isConfirming ? (
          <span className="member-chip-confirm">
            <button
              className="tiny-icon-button"
              onClick={() => void onConfirmRemove()}
              title={t('removeMember')}
              type="button"
            >
              <Icon name="check" />
            </button>
            <button
              className="tiny-icon-button"
              onClick={onCancelRemove}
              title={t('cancel')}
              type="button"
            >
              <Icon name="close" />
            </button>
          </span>
        ) : (
          <button
            className="tiny-icon-button member-chip-remove"
            onClick={onRequestRemove}
            title={t('removeMember')}
            type="button"
          >
            <Icon name="close" />
          </button>
        )
      ) : null}
    </span>
  );
}

function ShareDialog({
  existingTaskShares,
  focusedTaskShareId = null,
  focusedWorkspaceMemberId = null,
  onClose,
  onCreate,
  onRemoveWorkspaceMember,
  onRevokeTaskShare,
  onRevokeWorkspaceInvitation,
  onUpdateTaskShareRole,
  onUpdateWorkspaceMemberRole,
  pendingInvitations,
  roleMode,
  t,
  title,
  workspaceMembers = [],
}: {
  existingTaskShares: TaskItemShareResponse[];
  focusedTaskShareId?: string | null;
  focusedWorkspaceMemberId?: string | null;
  onClose: () => void;
  onCreate: (
    email: string,
    role: string,
  ) => Promise<{ token: string | null; expiresAt: string }>;
  onRemoveWorkspaceMember?: (userId: string) => Promise<void>;
  onRevokeTaskShare?: (shareId: string) => Promise<void>;
  onRevokeWorkspaceInvitation?: (id: string) => Promise<void>;
  onUpdateTaskShareRole?: (
    shareId: string,
    requestBody: UpdateTaskShareRequest,
  ) => Promise<unknown>;
  onUpdateWorkspaceMemberRole?: (
    userId: string,
    requestBody: UpdateWorkspaceMemberRequest,
  ) => Promise<unknown>;
  pendingInvitations: WorkspaceInvitationResponse[];
  roleMode: 'task' | 'workspace';
  t: Translate;
  title: string;
  workspaceMembers?: WorkspaceMemberResponse[];
}) {
  const [shareEmail, setShareEmail] = useState('');
  const [shareRole, setShareRole] = useState('Member');
  const [createdLink, setCreatedLink] = useState<string | null>(null);
  const [copiedText, setCopiedText] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const visibleTaskShares = existingTaskShares.filter((share) => !share.revokedAt);
  const visibleWorkspaceMembers = workspaceMembers;

  const copyShareUrl = async (value: string) => {
    await copyTextToClipboard(value);
    setCopiedText(true);
  };

  const submitShare = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const trimmedEmail = shareEmail.trim();

    if (!trimmedEmail) {
      return;
    }

    setError(null);
    setIsSubmitting(true);

    try {
      const created = await onCreate(
        trimmedEmail,
        roleMode === 'task'
          ? shareRole === 'ReadOnly' ? 'Viewer' : 'Editor'
          : shareRole);
      if (created.token) {
        setCreatedLink(buildShareUrl(created.token));
      }
      setShareEmail('');
      setCopiedText(false);
    } catch (shareError) {
      setError(getErrorMessage(shareError));
    } finally {
      setIsSubmitting(false);
    }
  };

  const updateWorkspaceMemberRole = async (
    userId: string,
    role: WorkspaceMembershipRole,
  ) => {
    if (!onUpdateWorkspaceMemberRole) {
      return;
    }

    setError(null);
    try {
      await onUpdateWorkspaceMemberRole(userId, { role });
    } catch (updateError) {
      setError(getErrorMessage(updateError));
    }
  };

  const updateTaskShareRole = async (
    shareId: string,
    role: 'Member' | 'ReadOnly',
  ) => {
    if (!onUpdateTaskShareRole) {
      return;
    }

    const nextRole = role === 'ReadOnly' ? 'Viewer' : 'Editor';
    setError(null);
    try {
      await onUpdateTaskShareRole(shareId, { role: nextRole as TaskItemShareRole });
    } catch (updateError) {
      setError(getErrorMessage(updateError));
    }
  };

  return (
    <ModalFrame className="dialog-backdrop share-dialog-backdrop" onClose={onClose}>
      <section
        aria-labelledby="share-dialog-title"
        aria-modal="true"
        className="workspace-invite-dialog share-dialog"
        role="dialog"
      >
        <div className="dialog-header">
          <div>
            <p className="detail-kicker">{t('sharing')}</p>
            <h2 id="share-dialog-title">{title}</h2>
          </div>
          <button className="icon-button" onClick={onClose} type="button">
            <Icon name="close" />
            <span className="sr-only">{t('close')}</span>
          </button>
        </div>

        <form className="workspace-invite-form share-dialog-form" onSubmit={(event) => void submitShare(event)}>
          <input
            aria-label={t('inviteEmail')}
            autoFocus
            onChange={(event) => setShareEmail(event.target.value)}
            placeholder={t('inviteEmail')}
            type="email"
            value={shareEmail}
          />
          <select
            aria-label={t('shareRole')}
            className="share-role-select"
            onChange={(event) => setShareRole(event.target.value)}
            value={shareRole}
          >
            <option value="Member">{t('roleMember')}</option>
            <option value="ReadOnly">{t('roleReadOnly')}</option>
          </select>
          <button className="icon-button" disabled={!shareEmail.trim() || isSubmitting} type="submit">
            <Icon name="check" />
          </button>
          <button className="icon-button" onClick={onClose} type="button">
            <Icon name="close" />
          </button>
        </form>

        {createdLink ? (
          <div className="invite-token-panel">
            <small>{t('shareLinkHelp')}</small>
            <button
              className="invite-token"
              onClick={() => void copyShareUrl(createdLink)}
              type="button"
            >
              {createdLink}
            </button>
            {copiedText ? (
              <small className="copied-feedback">{t('copiedToClipboard')}</small>
            ) : null}
          </div>
        ) : null}

        {error ? <p className="form-error">{error}</p> : null}

        {visibleWorkspaceMembers.length > 0 ||
        pendingInvitations.length > 0 ||
        visibleTaskShares.length > 0 ? (
          <div className="pending-invite-list share-dialog-list">
            {visibleWorkspaceMembers.map((member) => {
              const isOwner = isOwnerRole(member.role);

              return (
                <div
                  className="share-person-row"
                  data-focused={focusedWorkspaceMemberId === member.userId}
                  key={member.userId}
                >
                  <Icon name={isOwner ? 'crown' : 'user'} />
                  <span className="share-person-copy">
                    <strong>{member.displayName || member.email}</strong>
                    <small>{member.email}</small>
                  </span>
                  <select
                    aria-label={t('shareRole')}
                    className="share-role-select"
                    disabled={isOwner || !onUpdateWorkspaceMemberRole}
                    onChange={(event) =>
                      void updateWorkspaceMemberRole(
                        member.userId,
                        event.target.value as WorkspaceMembershipRole,
                      )}
                    value={isReadOnlyRole(member.role) ? 'ReadOnly' : isOwner ? 'Owner' : 'Member'}
                  >
                    {isOwner ? <option value="Owner">{t('roleOwner')}</option> : null}
                    <option value="Member">{t('roleMember')}</option>
                    <option value="ReadOnly">{t('roleReadOnly')}</option>
                  </select>
                  {!isOwner && onRemoveWorkspaceMember ? (
                    <button
                      className="tiny-icon-button"
                      onClick={() => void onRemoveWorkspaceMember(member.userId)}
                      title={t('removeMember')}
                      type="button"
                    >
                      <Icon name="close" />
                    </button>
                  ) : null}
                </div>
              );
            })}
            {pendingInvitations.map((invitation) => (
              <span
                className="pending-invite-chip"
                key={invitation.id}
                title={`${invitation.email} - ${formatDateTime(invitation.expiresAt)}`}
              >
                <Icon name="mail" />
                <span>{invitation.email}</span>
                <small>{formatWorkspaceRole(invitation.role, t)} - {t('pendingInvites')}</small>
                {onRevokeWorkspaceInvitation ? (
                  <button
                    className="tiny-icon-button"
                    onClick={() => void onRevokeWorkspaceInvitation(invitation.id)}
                    title={t('revokeInvite')}
                    type="button"
                  >
                    <Icon name="close" />
                  </button>
                ) : null}
              </span>
            ))}
            {visibleTaskShares.map((share) => (
              <div
                className="share-person-row"
                data-focused={focusedTaskShareId === share.id}
                key={share.id}
                title={`${share.email}${share.expiresAt ? ` - ${formatDateTime(share.expiresAt)}` : ''}`}
              >
                <Icon name={share.acceptedAt ? 'user' : 'mail'} />
                <span className="share-person-copy">
                  <strong>{share.email}</strong>
                  <small>{share.acceptedAt ? t('sharedWith') : t('pendingInvites')}</small>
                </span>
                <select
                  aria-label={t('shareRole')}
                  className="share-role-select"
                  disabled={!onUpdateTaskShareRole}
                  onChange={(event) =>
                    void updateTaskShareRole(
                      share.id,
                      event.target.value as 'Member' | 'ReadOnly',
                    )}
                  value={isReadOnlyTaskShareRole(share.role) ? 'ReadOnly' : 'Member'}
                >
                  <option value="Member">{t('roleMember')}</option>
                  <option value="ReadOnly">{t('roleReadOnly')}</option>
                </select>
                {onRevokeTaskShare ? (
                  <button
                    className="tiny-icon-button"
                    onClick={() => void onRevokeTaskShare(share.id)}
                    title={t('removeShare')}
                    type="button"
                  >
                    <Icon name="close" />
                  </button>
                ) : null}
              </div>
            ))}
          </div>
        ) : (
          <p className="context-muted share-dialog-empty">{t('notShared')}</p>
        )}
      </section>
    </ModalFrame>
  );
}

function FloatingBoardActions({
  archiveModeIsActive,
  canCreateTask,
  canManageSharing,
  canPermanentlyDelete,
  colorOptions,
  editModeIsEnabled,
  onBatchUpdate,
  onCopyTaskItemsToWorkspace,
  onOpenCreateTask,
  onOpenBatchArchive,
  onOpenBatchReopen,
  onOpenBatchPermanentDelete,
  onOpenBatchShare,
  onToggleEditMode,
  projects,
  selectedTaskCount,
  statusOptions,
  taskCount,
  t,
  workspaces,
}: {
  archiveModeIsActive: boolean;
  canCreateTask: boolean;
  canManageSharing: boolean;
  canPermanentlyDelete: boolean;
  colorOptions: string[];
  editModeIsEnabled: boolean;
  onBatchUpdate: (requestBody: UpdateTaskItemRequest) => Promise<void>;
  onCopyTaskItemsToWorkspace: (workspaceId: string) => Promise<void>;
  onOpenCreateTask: () => void;
  onOpenBatchArchive: () => void;
  onOpenBatchReopen: () => void;
  onOpenBatchPermanentDelete: () => void;
  onOpenBatchShare: () => void;
  onToggleEditMode: () => void;
  projects: ProjectResponse[];
  selectedTaskCount: number;
  statusOptions: string[];
  taskCount: number;
  t: Translate;
  workspaces: WorkspaceResponse[];
}) {
  const [isOpen, setIsOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const openActions = () => {
      setIsOpen(true);
    };

    window.addEventListener('dumptether:open-actions', openActions);

    return () => window.removeEventListener('dumptether:open-actions', openActions);
  }, []);

  useEffect(() => {
    if (!isOpen) {
      return undefined;
    }

    const handlePointerDown = (event: PointerEvent) => {
      if (
        menuRef.current &&
        event.target instanceof Node &&
        !menuRef.current.contains(event.target) &&
        !editModeIsEnabled
      ) {
        setIsOpen(false);
      }
    };

    window.addEventListener('pointerdown', handlePointerDown);

    return () => window.removeEventListener('pointerdown', handlePointerDown);
  }, [editModeIsEnabled, isOpen]);

  return (
    <div className="floating-board-actions" data-edit-mode={editModeIsEnabled} ref={menuRef}>
      <button
        className="quick-create-fab"
        data-active={isOpen}
        onClick={() => setIsOpen((open) => !open)}
        title={editModeIsEnabled
          ? `${selectedTaskCount} ${t('selectedTasks')}`
          : archiveModeIsActive ? t('archiveActions') : t('newTask')}
        type="button"
      >
        <Icon name={editModeIsEnabled ? 'check' : 'plus'} />
        <span>{editModeIsEnabled
          ? `${selectedTaskCount} ${t('selectedTasks')}`
          : archiveModeIsActive ? t('archiveActions') : t('newTask')}</span>
      </button>

      {isOpen ? (
        <div className="quick-action-menu">
          {canCreateTask ? (
            <button
              onClick={() => {
                setIsOpen(false);
                onOpenCreateTask();
              }}
              type="button"
            >
              <Icon name="plus" />
              <span>{t('addTask')}</span>
              <kbd>Alt+N</kbd>
            </button>
          ) : null}
          {editModeIsEnabled ? (
            <>
              <span className="quick-action-menu-label">
                {selectedTaskCount} {t('selectedTasks')}
              </span>
              {archiveModeIsActive ? (
                <>
                  <button
                    disabled={selectedTaskCount === 0}
                    onClick={() => {
                      onOpenBatchReopen();
                      setIsOpen(false);
                    }}
                    type="button"
                  >
                    <Icon name="undo" />
                    <span>{t('unarchiveSelected')}</span>
                  </button>
                  {canPermanentlyDelete ? (
                    <button
                      className="danger-action"
                      disabled={selectedTaskCount === 0}
                      onClick={() => {
                        onOpenBatchPermanentDelete();
                        setIsOpen(false);
                      }}
                      type="button"
                    >
                      <Icon name="trash" />
                      <span>{t('deletePermanently')}</span>
                    </button>
                  ) : null}
                </>
              ) : (
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
              )}
              {canManageSharing && !archiveModeIsActive ? (
                <button
                  disabled={selectedTaskCount === 0}
                  onClick={() => {
                    onOpenBatchShare();
                    setIsOpen(false);
                  }}
                  type="button"
                >
                  <Icon name="users" />
                  <span>{t('shareSelected')}</span>
                </button>
              ) : null}
              <div className="batch-action-grid" aria-label={`${selectedTaskCount} ${t('selectedTasks')}`}>
                <select
                  aria-label={t('copyToBoard')}
                  disabled={selectedTaskCount === 0 || workspaces.length === 0}
                  onChange={(event) => {
                    if (event.target.value) {
                      void onCopyTaskItemsToWorkspace(event.target.value);
                      setIsOpen(false);
                    }
                  }}
                  value=""
                >
                  <option value="">{t('copyToBoard')}</option>
                  {workspaces.map((workspace) => (
                    <option key={workspace.id} value={workspace.id}>
                      {formatWorkspaceName(workspace.name, t)}
                    </option>
                  ))}
                </select>
                {!archiveModeIsActive ? (
                  <>
                    <select
                      aria-label={t('changeStatus')}
                      disabled={selectedTaskCount === 0}
                      onChange={(event) => {
                        if (event.target.value) {
                          void onBatchUpdate({ status: event.target.value });
                          setIsOpen(false);
                        }
                      }}
                      value=""
                    >
                      <option value="">{t('changeStatus')}</option>
                      {statusOptions.map((status) => (
                        <option key={status} value={status}>
                          {status}
                        </option>
                      ))}
                    </select>
                    <select
                      aria-label={t('changeCategory')}
                      disabled={selectedTaskCount === 0}
                      onChange={(event) => {
                        const project = projects.find((candidate) => candidate.id === event.target.value);
                        if (project) {
                          void onBatchUpdate({
                            projectId: project.id,
                            category: project.name,
                          });
                          setIsOpen(false);
                        }
                      }}
                      value=""
                    >
                      <option value="">{t('changeCategory')}</option>
                      {projects.map((project) => (
                        <option key={project.id} value={project.id}>
                          {project.name}
                        </option>
                      ))}
                    </select>
                    <ColorOptionPicker
                      emptyLabel={t('noTaskColors')}
                      label={t('changeColor')}
                      onChange={(color) => {
                        void onBatchUpdate({ color: color || null });
                        setIsOpen(false);
                      }}
                      options={colorOptions}
                      value=""
                      zeroLabel={t('changeColor')}
                    />
                    <input
                      aria-label={t('changeDueDate')}
                      disabled={selectedTaskCount === 0}
                      onChange={(event) => {
                        const followUpAt = event.target.value
                          ? new Date(`${event.target.value}T12:00:00`).toISOString()
                          : null;
                        void onBatchUpdate({ followUpAt });
                        setIsOpen(false);
                      }}
                      type="date"
                    />
                  </>
                ) : null}
              </div>
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
            taskCount > 0 ? (
              <button
                onClick={() => {
                  onToggleEditMode();
                  setIsOpen(false);
                }}
                type="button"
              >
                <Icon name="check" />
                <span>{t('selectTasksForAction')}</span>
                <kbd>Alt+X</kbd>
              </button>
            ) : null
          )}
        </div>
      ) : null}
    </div>
  );
}

function DraftTaskCard({
  onCancel,
  onCreateTaskItem,
  onCreated,
  projects,
  selectedProjectId,
  t,
  templates,
}: {
  onCancel: () => void;
  onCreateTaskItem: (
    title: string,
    options?: Partial<CreateTaskItemRequest>,
  ) => Promise<TaskItemDetailResponse | null>;
  onCreated: (taskItem: TaskItemDetailResponse) => void;
  projects: ProjectResponse[];
  selectedProjectId: string;
  t: Translate;
  templates: TaskTemplateDetailResponse[];
}) {
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
      style={getTaskCardStyle('#FFF3A6')}
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
              <p className="detail-kicker">{t('newTask')}</p>
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

function TaskDetail({
  archiveDialogIsOpen,
  archiveResolutions,
  canManageSharing,
  colorOptions,
  onAddTimelineEntry,
  onArchive,
  onClose,
  onCloseArchiveDialog,
  onOpenArchiveDialog,
  onReopen,
  onCreateTaskShareLink,
  onQueueDeleteTimelineEntry,
  onRevokeTaskShare,
  onUndoDeleteTimelineEntry,
  onUpdateFieldValues,
  onUpdateTaskItem,
  onUpdateTaskShareRole,
  onUpdateTimelineEntry,
  pendingDeletedNoteIds,
  projects,
  statusOptions,
  t,
  taskItem,
}: {
  archiveDialogIsOpen: boolean;
  archiveResolutions: ArchiveResolutionResponse[];
  canManageSharing: boolean;
  colorOptions: string[];
  onAddTimelineEntry: (note: string, fieldValues?: FieldValueMap) => Promise<void>;
  onArchive: (requestBody: ArchiveTaskItemRequest) => Promise<void>;
  onClose: () => Promise<void>;
  onCloseArchiveDialog: () => void;
  onOpenArchiveDialog: () => void;
  onReopen: (note?: string) => Promise<void>;
  onCreateTaskShareLink: (
    taskItemId: string,
    requestBody: CreateTaskShareRequest,
  ) => Promise<TaskShareLinkResponse>;
  onQueueDeleteTimelineEntry: (entryId: string) => void;
  onRevokeTaskShare: (taskItemId: string, shareId: string) => Promise<void>;
  onUndoDeleteTimelineEntry: (entryId: string) => void;
  onUpdateFieldValues: (fieldValues: FieldValueMap) => Promise<void>;
  onUpdateTaskItem: (requestBody: UpdateTaskItemRequest) => Promise<void>;
  onUpdateTaskShareRole: (
    taskItemId: string,
    shareId: string,
    requestBody: UpdateTaskShareRequest,
  ) => Promise<TaskItemDetailResponse>;
  onUpdateTimelineEntry: (
    entryId: string,
    note: string | null,
    fieldValues?: FieldValueMap,
  ) => Promise<void>;
  pendingDeletedNoteIds: string[];
  projects: ProjectResponse[];
  statusOptions: string[];
  t: Translate;
  taskItem: TaskItemDetailResponse;
}) {
  const [reopenNote, setReopenNote] = useState('');
  const [fieldDraft, setFieldDraft] = useState<FieldValueMap>({});
  const [isSavingFields, setIsSavingFields] = useState(false);
  const fieldSaveTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const lastSavedFieldDraftRef = useRef('');
  const headerFields = useMemo(
    () => taskItem.template?.fields.filter((field) => field.scope === 'Header') ?? [],
    [taskItem.template],
  );
  const entryFields = useMemo(
    () => taskItem.template?.fields.filter((field) => field.scope === 'Entry') ?? [],
    [taskItem.template],
  );

  useEffect(() => {
    setReopenNote('');
    const nextFieldDraft = toFieldValueMap(taskItem.fieldValues);
    setFieldDraft(nextFieldDraft);
    lastSavedFieldDraftRef.current = JSON.stringify(
      withDefaultFieldValues(headerFields, nextFieldDraft),
    );
  }, [taskItem, headerFields]);

  const headerFieldsCanBeEdited = !taskItem.archivedAt && headerFields.length > 0;

  useEffect(() => {
    if (!headerFieldsCanBeEdited) {
      return undefined;
    }

    const nextFieldValues = withDefaultFieldValues(headerFields, fieldDraft);
    const serializedValues = JSON.stringify(nextFieldValues);

    if (serializedValues === lastSavedFieldDraftRef.current) {
      return undefined;
    }

    if (fieldSaveTimerRef.current) {
      clearTimeout(fieldSaveTimerRef.current);
    }

    fieldSaveTimerRef.current = setTimeout(() => {
      setIsSavingFields(true);
      void onUpdateFieldValues(nextFieldValues)
        .then(() => {
          lastSavedFieldDraftRef.current = serializedValues;
        })
        .finally(() => {
          setIsSavingFields(false);
        });
    }, 500);

    return () => {
      if (fieldSaveTimerRef.current) {
        clearTimeout(fieldSaveTimerRef.current);
      }
    };
  }, [fieldDraft, headerFields, headerFieldsCanBeEdited, onUpdateFieldValues]);
  const closeFromHeader = (event: MouseEvent<HTMLDivElement>) => {
    if (
      event.target instanceof HTMLElement &&
      event.target.closest(
        'button, input, select, textarea, label, .color-popover, .task-share-popover, .share-dialog, .task-header-fields, .task-meta-chip, .member-chip, .share-chip, .pending-invite-chip, .category-multi-select',
      )
    ) {
      return;
    }

    void onClose();
  };

  return (
    <section className="task-detail" aria-label="Task detail">
      <div
        className="detail-header task-detail-header"
        onClick={closeFromHeader}
        style={getTaskCardStyle(taskItem.color)}
      >
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
          statusOptions={statusOptions}
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
              placement="leftWide"
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
        {canManageSharing ? (
          <div className="task-detail-share-corner">
            <TaskShareStrip
              onCreateTaskShareLink={onCreateTaskShareLink}
              onRevokeTaskShare={onRevokeTaskShare}
              onUpdateTaskShareRole={onUpdateTaskShareRole}
              t={t}
              taskItem={taskItem}
            />
          </div>
        ) : null}
      </div>

      {headerFields.length > 0 ? (
      <section className="detail-section fields-details task-header-fields-section">
        <div className="section-heading">
          <span>
            <h3 id="fields-title">{t('taskFields')}</h3>
          </span>
          {isSavingFields ? (
            <span
              aria-label={t('saving')}
              className="fields-saving saving-copy"
              data-state="saving"
              role="status"
              title={t('saving')}
            />
          ) : null}
        </div>

        {headerFieldsCanBeEdited ? (
          <FieldEditorList
            fields={headerFields}
            onChange={(fieldId, value) =>
              setFieldDraft((currentValues) => ({
                ...currentValues,
                [fieldId]: value,
              }))
            }
            values={fieldDraft}
          />
        ) : (
          <FieldValueList fields={headerFields} fieldValues={taskItem.fieldValues} />
        )}
      </section>
      ) : null}

      <TimelinePanel
        entryFields={entryFields}
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

function TaskShareStrip({
  onCreateTaskShareLink,
  onRevokeTaskShare,
  onUpdateTaskShareRole,
  t,
  taskItem,
}: {
  onCreateTaskShareLink: (
    taskItemId: string,
    requestBody: CreateTaskShareRequest,
  ) => Promise<TaskShareLinkResponse>;
  onRevokeTaskShare: (taskItemId: string, shareId: string) => Promise<void>;
  onUpdateTaskShareRole: (
    taskItemId: string,
    shareId: string,
    requestBody: UpdateTaskShareRequest,
  ) => Promise<TaskItemDetailResponse>;
  t: Translate;
  taskItem: TaskItemDetailResponse;
}) {
  const [isOpen, setIsOpen] = useState(false);
  const [focusedTaskShareId, setFocusedTaskShareId] = useState<string | null>(null);
  const menuRef = useRef<HTMLDivElement>(null);
  const visibleShares = taskItem.shares.filter((share) => !share.revokedAt);

  return (
    <div
      className="task-share-popover"
      onClick={(event) => event.stopPropagation()}
      onPointerDown={(event) => event.stopPropagation()}
      ref={menuRef}
    >
      <div className="member-chip-strip task-share-strip" aria-label={t('sharing')}>
        {visibleShares.slice(0, 3).map((share) => (
          <button
            className="member-chip share-person-chip"
            key={share.id}
            onClick={(event) => {
              event.stopPropagation();
              setFocusedTaskShareId(share.id);
              setIsOpen(true);
            }}
            title={`${share.email} - ${formatTaskShareRole(share.role, t)}`}
            type="button"
          >
            <Icon name={share.acceptedAt ? 'user' : 'mail'} />
            <span>{share.email}</span>
          </button>
        ))}
        {visibleShares.length > 3 ? (
          <button
            className="member-chip"
            onClick={(event) => {
              event.stopPropagation();
              setFocusedTaskShareId(null);
              setIsOpen(true);
            }}
            type="button"
          >
            +{visibleShares.length - 3}
          </button>
        ) : null}
        <button
          className="tiny-icon-button"
          onClick={(event) => {
            event.stopPropagation();
            setFocusedTaskShareId(null);
            setIsOpen((open) => !open);
          }}
          title={t('shareTask')}
          type="button"
        >
          <Icon name="plus" />
        </button>
      </div>

      {isOpen ? (
        <ShareDialog
          existingTaskShares={taskItem.shares}
          focusedTaskShareId={focusedTaskShareId}
          onClose={() => setIsOpen(false)}
          onCreate={async (email, role) =>
            await onCreateTaskShareLink(taskItem.id, {
              email,
              role: role as TaskItemShareRole,
            })}
          onRevokeTaskShare={(shareId) => onRevokeTaskShare(taskItem.id, shareId)}
          onUpdateTaskShareRole={(shareId, requestBody) =>
            onUpdateTaskShareRole(taskItem.id, shareId, requestBody)}
          pendingInvitations={[]}
          roleMode="task"
          t={t}
          title={taskItem.title}
        />
      ) : null}
    </div>
  );
}

function TaskHeaderEditor({
  onUpdateTaskItem,
  projects,
  statusOptions,
  t,
  taskItem,
}: {
  onUpdateTaskItem: (requestBody: UpdateTaskItemRequest) => Promise<void>;
  projects: ProjectResponse[];
  statusOptions: string[];
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
  const editingFieldRef = useRef<typeof editingField>(null);
  const selectedCategoryNames = splitTaskCategories(category);
  const displayedProjects = getProjectsForTaskCategories(taskItem.category ?? category, projects);
  const displayedProject = taskItem.projectId
    ? projects.find((project) => project.id === taskItem.projectId) ?? displayedProjects[0] ?? null
    : displayedProjects[0] ?? null;
  const displayedCategoryLabel = splitTaskCategories(taskItem.category).join(', ') || t('noCategory');

  useEffect(() => {
    setTitle(taskItem.title);
    setStatus(taskItem.status ?? '');
    setCategory(taskItem.category ?? '');
    const taskCategoryNames = splitTaskCategories(taskItem.category);
    setCategoryProjectId(
      taskItem.projectId ??
      projects.find((project) =>
        taskCategoryNames.some((categoryName) =>
          categoryName.toLowerCase() === project.name.toLowerCase()))?.id ??
      '',
    );
    setFollowUpDate(toDateInputValue(taskItem.followUpAt));
    setSaveState('idle');
  }, [projects, taskItem]);

  useEffect(() => {
    setEditingField(null);
  }, [taskItem.id]);

  useEffect(() => {
    editingFieldRef.current = editingField;
  }, [editingField]);

  const clearEditingField = (field?: typeof editingField) => {
    if (!field || editingFieldRef.current === field) {
      setEditingField(null);
    }
  };

  const saveChanges = async (overrides: Partial<{
    title: string;
    status: string;
    category: string;
    projectId: string | null;
    followUpDate: string;
  }> = {}, options: { field?: typeof editingField; keepEditing?: boolean } = {}) => {
    if (taskItem.archivedAt) {
      return;
    }

    const nextTitle = (overrides.title ?? title).trim();
    const nextStatus = (overrides.status ?? status).trim();
    const nextCategory = joinTaskCategories(splitTaskCategories(overrides.category ?? category)) ?? '';
    const nextProjectId = Object.prototype.hasOwnProperty.call(overrides, 'projectId')
      ? overrides.projectId
      : Object.prototype.hasOwnProperty.call(overrides, 'category')
        ? getPrimaryProjectIdForCategories(nextCategory, projects)
        : categoryProjectId;
    const nextFollowUpDate = overrides.followUpDate ?? followUpDate;
    const normalizedFollowUpAt = nextFollowUpDate
      ? new Date(`${nextFollowUpDate}T12:00:00`).toISOString()
      : null;
    const normalizedNextProjectId = nextProjectId ?? '';
    const normalizedCurrentProjectId = taskItem.projectId ?? '';

    if (!nextTitle) {
      setTitle(taskItem.title);
      return;
    }

    const hasChanges =
      nextTitle !== taskItem.title ||
      nextStatus !== (taskItem.status ?? '') ||
      nextCategory !== (joinTaskCategories(splitTaskCategories(taskItem.category)) ?? '') ||
      normalizedNextProjectId !== normalizedCurrentProjectId ||
      normalizedFollowUpAt !== taskItem.followUpAt;

    if (!hasChanges) {
      if (!options.keepEditing) {
        clearEditingField(options.field);
      }
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
      if (!options.keepEditing) {
        clearEditingField(options.field);
      }
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
      setCategoryProjectId(
        taskItem.projectId ??
        getPrimaryProjectIdForCategories(taskItem.category ?? '', projects) ??
        '',
      );
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
          {splitTaskCategories(taskItem.category).length > 0 ? (
            splitTaskCategories(taskItem.category).map((categoryName) => {
              const project = projects.find((candidate) =>
                candidate.name.toLowerCase() === categoryName.toLowerCase()) ?? null;

              return (
                <span key={categoryName} style={getContextChipStyle(project?.color ?? null)}>
                  {t('category')}: {categoryName}
                </span>
              );
            })
          ) : (
            <span>{t('category')}: {t('noCategory')}</span>
          )}
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
            onBlur={() => void saveChanges({}, { field: 'title' })}
            onChange={(event) => setTitle(event.target.value)}
            onKeyDown={handleTextKeyDown}
            required
            type="text"
            value={title}
          />
        ) : (
          <button
            className="heading-edit-trigger task-heading-trigger"
            onClick={(event) => {
              event.stopPropagation();
              setEditingField('title');
            }}
            title={t('editTask')}
            type="button"
          >
            <h2>{taskItem.title}</h2>
          </button>
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
          <select
            aria-label={t('status')}
            autoFocus
            disabled={isSubmitting}
            onBlur={() => void saveChanges({}, { field: 'status' })}
            onChange={(event) => {
              setStatus(event.target.value);
              void saveChanges({ status: event.target.value }, { field: 'status' });
            }}
            value={status}
          >
            <option value="">{t('noStatus')}</option>
            {statusOptions.map((statusOption) => (
              <option key={statusOption} value={statusOption}>
                {statusOption}
              </option>
            ))}
          </select>
        ) : (
          <button
            className="task-meta-chip"
            onClick={(event) => {
              event.stopPropagation();
              setEditingField('status');
            }}
            type="button"
          >
            {t('status')}: {taskItem.status ?? t('noStatus')}
          </button>
        )}
        {editingField === 'category' ? (
          <CategoryMultiSelect
            disabled={isSubmitting}
            onCancel={() => setEditingField(null)}
            onCommit={(nextCategories) => {
              const nextCategory = joinTaskCategories(nextCategories) ?? '';
              const nextProjectId = getPrimaryProjectIdForCategories(nextCategory, projects);
              setCategory(nextCategory);
              setCategoryProjectId(nextProjectId ?? '');
              void saveChanges(
                {
                  category: nextCategory,
                  projectId: nextProjectId,
                },
                { field: 'category' },
              );
            }}
            projects={projects}
            selectedCategories={selectedCategoryNames}
            t={t}
          />
        ) : (
          <button
            className="task-meta-chip"
            onClick={(event) => {
              event.stopPropagation();
              setEditingField('category');
            }}
            style={getContextChipStyle(displayedProject?.color ?? null)}
            type="button"
          >
            {t('category')}: {displayedCategoryLabel}
          </button>
        )}
        {editingField === 'followUp' ? (
          <input
            aria-label={t('followUpDate')}
            autoFocus
            disabled={isSubmitting}
            onBlur={() => void saveChanges({}, { field: 'followUp' })}
            onChange={(event) => {
              setFollowUpDate(event.target.value);
              void saveChanges({ followUpDate: event.target.value }, { field: 'followUp' });
            }}
            type="date"
            value={followUpDate}
          />
        ) : (
          <button
            className="task-meta-chip follow-up-chip"
            data-tone={getFollowUpTone(taskItem.followUpAt)}
            onClick={(event) => {
              event.stopPropagation();
              setEditingField('followUp');
            }}
            type="button"
          >
            {t('followUpDate')}: {taskItem.followUpAt ? formatFullDate(taskItem.followUpAt) : t('noFollowUp')}
          </button>
        )}
        {saveState !== 'idle' ? (
          <span
            aria-label={saveState === 'error' ? t('saveFailed') : t('saved')}
            className="saving-copy"
            data-state={saveState}
            role="status"
            title={saveState === 'error' ? t('saveFailed') : t('saved')}
          />
        ) : null}
      </div>
    </div>
  );
}

function CategoryMultiSelect({
  disabled,
  onCancel,
  onCommit,
  projects,
  selectedCategories,
  t,
}: {
  disabled: boolean;
  onCancel: () => void;
  onCommit: (categories: string[]) => void;
  projects: ProjectResponse[];
  selectedCategories: string[];
  t: Translate;
}) {
  const [draftCategories, setDraftCategories] = useState(selectedCategories);
  const pickerRef = useRef<HTMLDivElement>(null);
  const selectedNames = new Set(draftCategories.map((category) => category.toLowerCase()));

  useEffect(() => {
    setDraftCategories(selectedCategories);
  }, [selectedCategories]);

  useEffect(() => {
    const handlePointerDown = (event: PointerEvent) => {
      if (
        pickerRef.current &&
        event.target instanceof Node &&
        !pickerRef.current.contains(event.target)
      ) {
        onCommit(draftCategories);
      }
    };

    const handleKeyDown = (event: globalThis.KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault();
        onCancel();
      }
    };

    window.addEventListener('pointerdown', handlePointerDown);
    window.addEventListener('keydown', handleKeyDown);

    return () => {
      window.removeEventListener('pointerdown', handlePointerDown);
      window.removeEventListener('keydown', handleKeyDown);
    };
  }, [draftCategories, onCancel, onCommit]);

  const toggleCategory = (project: ProjectResponse) => {
    const hasCategory = selectedNames.has(project.name.toLowerCase());
    const nextCategories = hasCategory
      ? draftCategories.filter((category) =>
        category.toLowerCase() !== project.name.toLowerCase())
      : [...draftCategories, project.name];

    setDraftCategories(nextCategories);
  };

  return (
    <div
      className="category-multi-select"
      onClick={(event) => event.stopPropagation()}
      ref={pickerRef}
    >
      <div className="category-option-list">
        {projects.length === 0 ? (
          <span className="context-muted">{t('noCategoriesYet')}</span>
        ) : (
          projects.map((project) => {
            const isSelected = selectedNames.has(project.name.toLowerCase());

            return (
              <button
                className="category-option"
                data-selected={isSelected}
                disabled={disabled}
                key={project.id}
                onClick={() => toggleCategory(project)}
                style={getContextChipStyle(project.color)}
                type="button"
              >
                <span className="category-option-check">
                  {isSelected ? <Icon name="check" /> : null}
                </span>
                <span>{project.name}</span>
              </button>
            );
          })
        )}
      </div>
    </div>
  );
}

function ColorPickerPopover({
  color,
  colorOptions,
  label,
  onChange,
  placement = 'below',
  t,
}: {
  color: string;
  colorOptions?: string[];
  label: string;
  onChange: (color: string) => void;
  placement?: 'below' | 'left' | 'leftWide';
  t: Translate;
}) {
  const [isOpen, setIsOpen] = useState(false);
  const [draftColor, setDraftColor] = useState('');
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

    setDraftColor(isHexColor(color) ? color.toUpperCase() : selectedColor);

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
  }, [color, isOpen, selectedColor]);

  const commitColor = () => {
    const nextColor = draftColor.trim().toUpperCase();
    onChange(isHexColor(nextColor) ? nextColor : '');
    setIsOpen(false);
  };

  const cancelColor = () => {
    setDraftColor(isHexColor(color) ? color.toUpperCase() : selectedColor);
    setIsOpen(false);
  };

  return (
    <div
      className="color-popover"
      data-placement={placement}
      onClick={(event) => event.stopPropagation()}
      onPointerDown={(event) => event.stopPropagation()}
      ref={popoverRef}
    >
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
                data-selected={draftColor.toUpperCase() === choice}
                key={choice}
                onClick={(event) => {
                  event.stopPropagation();
                  setDraftColor(choice);
                }}
                style={{ backgroundColor: choice }}
                type="button"
              />
            ))}
            <span className="color-popover-code">{draftColor || t('noColor')}</span>
          </div>
          <div className="custom-color-row">
            <input
              aria-label="Custom color"
              onChange={(event) => setDraftColor(event.target.value.toUpperCase())}
              onClick={(event) => event.stopPropagation()}
              onPointerDown={(event) => event.stopPropagation()}
              type="color"
              value={isHexColor(draftColor) ? draftColor : selectedColor}
            />
            <input
              aria-label={t('taskColor')}
              className="custom-color-input"
              onChange={(event) => setDraftColor(event.target.value.toUpperCase())}
              onClick={(event) => event.stopPropagation()}
              onPointerDown={(event) => event.stopPropagation()}
              placeholder="#FDE68A"
              type="text"
              value={draftColor}
            />
          </div>
          <div className="color-popover-actions">
            <button
              className="tiny-icon-button"
              disabled={!isHexColor(draftColor)}
              onClick={(event) => {
                event.stopPropagation();
                commitColor();
              }}
              title={t('saved')}
              type="button"
            >
              <Icon name="check" />
            </button>
            <button
              className="tiny-icon-button"
              onClick={(event) => {
                event.stopPropagation();
                cancelColor();
              }}
              title={t('cancel')}
              type="button"
            >
              <Icon name="close" />
            </button>
          </div>
          {color ? (
            <button
              className="clear-color-button"
              onClick={(event) => {
                event.stopPropagation();
                onChange('');
                setIsOpen(false);
              }}
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
                  {field.type} · R{field.layoutRow} C{field.layoutColumn}
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
                  {field.type} · R{field.layoutRow} C{field.layoutColumn}
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

function AuthPanel({
  authOptions,
  currentUser,
  isLoading,
  onDevelopmentLogin,
  onGuestLogin,
  onLogin,
  onLogout,
  onRegister,
  temporarySessionIsActive,
  t,
  variant,
}: {
  authOptions: AuthClientOptionsResponse;
  currentUser: CurrentUserResponse | null;
  isLoading: boolean;
  onDevelopmentLogin: () => Promise<void>;
  onGuestLogin: () => Promise<void>;
  onLogin: (requestBody: LoginUserRequest) => Promise<void>;
  onLogout?: () => Promise<void>;
  onRegister: (requestBody: RegisterUserRequest) => Promise<RegisterUserResponse>;
  temporarySessionIsActive: boolean;
  t: Translate;
  variant: 'gate' | 'settings';
}) {
  const [mode, setMode] = useState<'login' | 'register'>('login');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [statusMessage, setStatusMessage] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const submitAuthForm = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setFormError(null);
    setStatusMessage(null);
    setIsSubmitting(true);

    try {
      if (mode === 'register') {
        const registered = await onRegister({
          email: email.trim(),
          password,
          displayName: displayName.trim() || null,
        });
        setStatusMessage(
          registered.emailConfirmationRequired
            ? t('emailConfirmationSent')
            : t('authRegistered'),
        );
      } else {
        await onLogin({
          email: email.trim(),
          password,
          deviceName: 'web browser',
        });
        setStatusMessage(t('authLoggedIn'));
      }

      setPassword('');
    } catch (error) {
      setFormError(getErrorMessage(error));
    } finally {
      setIsSubmitting(false);
    }
  };

  const submitDevelopmentLogin = async () => {
    setFormError(null);
    setStatusMessage(null);
    setIsSubmitting(true);

    try {
      await onDevelopmentLogin();
      setStatusMessage(t('authLoggedIn'));
    } catch (error) {
      setFormError(getErrorMessage(error));
    } finally {
      setIsSubmitting(false);
    }
  };

  const submitGuestLogin = async () => {
    setFormError(null);
    setStatusMessage(null);
    setIsSubmitting(true);

    try {
      await onGuestLogin();
      setStatusMessage(t('guestModeToast'));
    } catch (error) {
      setFormError(getErrorMessage(error));
    } finally {
      setIsSubmitting(false);
    }
  };

  const wrapperClassName = variant === 'gate'
    ? 'auth-gate'
    : 'settings-section auth-panel';
  const canSubmit = email.trim().length > 0 && password.length >= 8;

  if (currentUser) {
    return (
      <section className={wrapperClassName} aria-label={t('account')}>
        <div className="auth-heading">
          <p className="detail-kicker">{t('account')}</p>
          <h2>{currentUser.user.displayName || currentUser.user.email}</h2>
          <p>{t('signedInAs')}: {currentUser.user.email}</p>
        </div>
        {temporarySessionIsActive ? (
          <p className="guest-warning">{t('guestModePersistent')}</p>
        ) : null}
        <div className="auth-workspace-list">
          {currentUser.workspaces.map((workspaceItem) => (
            <span className="auth-workspace-chip" key={workspaceItem.id}>
              <span
                className="workspace-color-dot"
                style={{ backgroundColor: workspaceItem.color ?? '#184c48' }}
              />
              {workspaceItem.name}
              <strong>{formatWorkspaceRole(workspaceItem.role, t)}</strong>
            </span>
          ))}
        </div>
        {onLogout ? (
          <button
            className="secondary-action logout-button"
            disabled={isSubmitting}
            onClick={async () => {
              setIsSubmitting(true);
              try {
                await onLogout();
              } catch (error) {
                setFormError(getErrorMessage(error));
              } finally {
                setIsSubmitting(false);
              }
            }}
            type="button"
          >
            <Icon name="logout" />
            {t('logout')}
          </button>
        ) : null}
        {formError ? <p className="form-error">{formError}</p> : null}
      </section>
    );
  }

  return (
    <section className={wrapperClassName} aria-label={t('account')}>
      <div className="auth-heading">
        <p className="detail-kicker">{t('account')}</p>
        <h2>{variant === 'gate' ? t('authRequiredTitle') : t('notSignedIn')}</h2>
        <p>{variant === 'gate' ? t('authRequiredBody') : t('authSettingsHelp')}</p>
      </div>

      <div className="auth-mode-toggle" role="group" aria-label={t('account')}>
        <button
          aria-pressed={mode === 'login'}
          onClick={() => setMode('login')}
          type="button"
        >
          {t('login')}
        </button>
        <button
          aria-pressed={mode === 'register'}
          onClick={() => setMode('register')}
          type="button"
        >
          {t('register')}
        </button>
      </div>

      {authOptions.oAuthProviders.length > 0 ? (
        <div className="oauth-login-list">
          {authOptions.oAuthProviders.map((provider) => (
            <button
              className="secondary-action"
              disabled={isSubmitting || isLoading}
              key={provider}
              onClick={() => beginOAuthLogin(provider)}
              type="button"
            >
              {formatOAuthProvider(provider, t)}
            </button>
          ))}
        </div>
      ) : null}

      <form className="auth-form" onSubmit={(event) => void submitAuthForm(event)}>
        {mode === 'register' ? (
          <label>
            {t('displayName')}
            <input
              autoComplete="name"
              onChange={(event) => setDisplayName(event.target.value)}
              type="text"
              value={displayName}
            />
          </label>
        ) : null}

        <label>
          {t('email')}
          <input
            autoComplete="email"
            onChange={(event) => setEmail(event.target.value)}
            required
            type="email"
            value={email}
          />
        </label>

        <label>
          {t('password')}
          <input
            autoComplete={mode === 'register' ? 'new-password' : 'current-password'}
            minLength={8}
            onChange={(event) => setPassword(event.target.value)}
            required
            type="password"
            value={password}
          />
          {mode === 'register' ? (
            <small className="form-help">{t('passwordRequirement')}</small>
          ) : null}
        </label>

        <button
          className="auth-submit-button"
          disabled={!canSubmit || isSubmitting || isLoading}
          type="submit"
        >
          <Icon name={mode === 'register' ? 'user' : 'login'} />
          {mode === 'register' ? t('registerButton') : t('loginButton')}
        </button>
      </form>

      {authOptions.developmentLoginEnabled ? (
        <div className="dev-login-panel">
          <button
            className="secondary-action"
            disabled={isSubmitting || isLoading}
            onClick={() => void submitDevelopmentLogin()}
            type="button"
          >
            {t('useDevelopmentAccount')}
          </button>
          <p>{t('developmentAccountHelp')}</p>
        </div>
      ) : null}

      {authOptions.guestSessionsEnabled ? (
        <div className="dev-login-panel">
          <button
            className="ghost-button auth-secondary-button"
            disabled={isSubmitting || isLoading}
            onClick={() => void submitGuestLogin()}
            type="button"
          >
            <Icon name="user" />
            {t('continueWithoutAccount')}
          </button>
          <p>{t('continueWithoutAccountHelp')}</p>
        </div>
      ) : null}

      {statusMessage ? <p className="form-success">{statusMessage}</p> : null}
      {formError ? <p className="form-error">{formError}</p> : null}
    </section>
  );
}

function AccountPanel({
  authOptions,
  currentUser,
  incomingTaskShares,
  incomingWorkspaceInvitations,
  isLoadingAuth,
  onAcceptIncomingWorkspaceInvitation,
  onClose,
  onDeclineIncomingWorkspaceInvitation,
  onDevelopmentLogin,
  onGuestLogin,
  onLeaveTaskShare,
  onLogin,
  onLogout,
  onRegister,
  temporarySessionIsActive,
  t,
}: {
  authOptions: AuthClientOptionsResponse;
  currentUser: CurrentUserResponse | null;
  incomingTaskShares: TaskShareInboxResponse[];
  incomingWorkspaceInvitations: WorkspaceInvitationInboxResponse[];
  isLoadingAuth: boolean;
  onAcceptIncomingWorkspaceInvitation: (id: string) => Promise<void>;
  onClose: () => void;
  onDeclineIncomingWorkspaceInvitation: (id: string) => Promise<void>;
  onDevelopmentLogin: () => Promise<void>;
  onGuestLogin: () => Promise<void>;
  onLeaveTaskShare: (shareId: string) => Promise<void>;
  onLogin: (requestBody: LoginUserRequest) => Promise<void>;
  onLogout: () => Promise<void>;
  onRegister: (requestBody: RegisterUserRequest) => Promise<RegisterUserResponse>;
  temporarySessionIsActive: boolean;
  t: Translate;
}) {
  return (
    <ModalFrame onClose={onClose}>
      <section
        aria-labelledby="account-title"
        aria-modal="true"
        className="account-panel"
        role="dialog"
      >
        <div className="dialog-header">
          <div>
            <p className="detail-kicker">DumpTether</p>
            <h2 id="account-title">{t('account')}</h2>
          </div>
          <button className="icon-button" onClick={onClose} type="button">
            <Icon name="close" />
            <span className="sr-only">{t('close')}</span>
          </button>
        </div>

        <AuthPanel
          authOptions={authOptions}
          currentUser={currentUser}
          isLoading={isLoadingAuth}
          onDevelopmentLogin={onDevelopmentLogin}
          onGuestLogin={onGuestLogin}
          onLogin={onLogin}
          onLogout={onLogout}
          onRegister={onRegister}
          temporarySessionIsActive={temporarySessionIsActive}
          t={t}
          variant="settings"
        />

        <section className="settings-section">
          <h3>{t('notifications')}</h3>
          {incomingWorkspaceInvitations.length === 0 && incomingTaskShares.length === 0 ? (
            <p>{t('noIncomingNotifications')}</p>
          ) : (
            <div className="account-notification-list">
              {incomingWorkspaceInvitations.map((invitation) => (
                <article className="account-notification-card" key={invitation.id}>
                  <span
                    className="workspace-color-dot"
                    style={{ backgroundColor: invitation.workspaceColor ?? '#184c48' }}
                  />
                  <div>
                    <strong>{invitation.workspaceName}</strong>
                    <p>
                      {t('invitedBy')} {invitation.invitedByDisplayName || invitation.invitedByEmail}
                      {' '}({formatWorkspaceRole(invitation.role, t)})
                    </p>
                  </div>
                  <div className="notification-actions">
                    <button
                      className="secondary-action"
                      onClick={() => void onAcceptIncomingWorkspaceInvitation(invitation.id)}
                      type="button"
                    >
                      <Icon name="check" />
                      {t('acceptInvite')}
                    </button>
                    <button
                      className="ghost-button"
                      onClick={() => void onDeclineIncomingWorkspaceInvitation(invitation.id)}
                      type="button"
                    >
                      {t('declineInvite')}
                    </button>
                  </div>
                </article>
              ))}
              {incomingTaskShares.map((share) => (
                <article className="account-notification-card" key={share.shareId}>
                  <span
                    className="workspace-color-dot"
                    style={{ backgroundColor: share.workspaceColor ?? '#184c48' }}
                  />
                  <div>
                    <strong>{share.taskTitle}</strong>
                    <p>
                      {t('sharedBy')} {share.sharedByDisplayName || share.sharedByEmail}
                      {' '} - {share.workspaceName}
                    </p>
                  </div>
                  <div className="notification-actions">
                    <button
                      className="ghost-button"
                      onClick={() => void onLeaveTaskShare(share.shareId)}
                      type="button"
                    >
                      {t('leaveTaskShare')}
                    </button>
                  </div>
                </article>
              ))}
            </div>
          )}
        </section>

        <section className="settings-section">
          <h3>{t('signInMethods')}</h3>
          <div className="auth-method-list">
            <div className="auth-method-card" data-state="ready">
              <Icon name="mail" />
              <div>
                <strong>{t('emailPasswordLogin')}</strong>
                <p>{t('emailPasswordLoginHelp')}</p>
              </div>
            </div>
            <div className="auth-method-card">
              <Icon name="cloud" />
              <div>
                <strong>{t('oauthLogin')}</strong>
                <p>{t('oauthLoginHelp')}</p>
              </div>
              <span>{t('configRequired')}</span>
            </div>
            <div className="auth-method-card">
              <Icon name="shield" />
              <div>
                <strong>{t('emailMfa')}</strong>
                <p>{t('emailMfaHelp')}</p>
              </div>
              <span>{t('configRequired')}</span>
            </div>
          </div>
        </section>
      </section>
    </ModalFrame>
  );
}

function SettingsPanel({
  archiveResolutions,
  configuredStatuses,
  language,
  onChangeLanguage,
  onClose,
  onCreateArchiveResolution,
  onDeleteArchiveResolution,
  onSaveStatusOptions,
  onUpdateArchiveResolution,
  t,
}: {
  archiveResolutions: ArchiveResolutionResponse[];
  configuredStatuses: string[];
  language: Language;
  onChangeLanguage: (language: Language) => void;
  onClose: () => void;
  onCreateArchiveResolution: (requestBody: CreateArchiveResolutionRequest) => Promise<void>;
  onDeleteArchiveResolution: (id: string) => Promise<void>;
  onSaveStatusOptions: (statuses: string[]) => void;
  onUpdateArchiveResolution: (
    id: string,
    requestBody: UpdateArchiveResolutionRequest,
  ) => Promise<void>;
  t: Translate;
}) {
  const [activeSection, setActiveSection] = useState<SettingsSectionKey>('general');
  const [statusDraft, setStatusDraft] = useState('');
  const [archiveReasonName, setArchiveReasonName] = useState('');
  const [archiveReasonRequiresNote, setArchiveReasonRequiresNote] = useState(false);
  const settingsSections: Array<{ key: SettingsSectionKey; label: string; icon: IconName }> = [
    { key: 'general', label: t('settingsGeneral'), icon: 'settings' },
    { key: 'statuses', label: t('statusOptions'), icon: 'status' },
    { key: 'archive', label: t('archiveReasons'), icon: 'archive' },
    { key: 'cleanup', label: t('cleanup'), icon: 'trash' },
  ];

  const addStatus = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const trimmedStatus = statusDraft.trim();

    if (!trimmedStatus) {
      return;
    }

    onSaveStatusOptions([...configuredStatuses, trimmedStatus]);
    setStatusDraft('');
  };

  const addArchiveReason = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const trimmedName = archiveReasonName.trim();

    if (!trimmedName) {
      return;
    }

    await onCreateArchiveResolution({
      name: trimmedName,
      requiresExplanation: archiveReasonRequiresNote,
    });
    setArchiveReasonName('');
    setArchiveReasonRequiresNote(false);
  };

  return (
    <ModalFrame onClose={onClose}>
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
            <span className="sr-only">{t('close')}</span>
          </button>
        </div>

        <div className="settings-layout">
          <nav className="settings-menu" aria-label={t('settingsSections')}>
            {settingsSections.map((section) => (
              <button
                aria-current={activeSection === section.key ? 'page' : undefined}
                key={section.key}
                onClick={() => setActiveSection(section.key)}
                type="button"
              >
                <Icon name={section.icon} />
                {section.label}
              </button>
            ))}
          </nav>

          <div className="settings-content">
            {activeSection === 'general' ? (
              <div className="settings-section settings-section-flat">
                <h3>{t('settingsGeneral')}</h3>
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
              </div>
            ) : null}

            {activeSection === 'statuses' ? (
              <div className="settings-section settings-section-flat">
                <h3>{t('statusOptions')}</h3>
                <form className="settings-inline-form" onSubmit={addStatus}>
                  <input
                    aria-label={t('addStatus')}
                    onChange={(event) => setStatusDraft(event.target.value)}
                    placeholder={t('addStatus')}
                    type="text"
                    value={statusDraft}
                  />
                  <button className="icon-button" disabled={!statusDraft.trim()} type="submit">
                    <Icon name="plus" />
                  </button>
                </form>
                <div className="settings-chip-list">
                  {configuredStatuses.map((status) => (
                    <span className="settings-chip" key={status}>
                      {status}
                      <button
                        className="tiny-icon-button"
                        onClick={() =>
                          onSaveStatusOptions(
                            configuredStatuses.filter((currentStatus) => currentStatus !== status),
                          )}
                        title={t('deleteNote')}
                        type="button"
                      >
                        <Icon name="trash" />
                      </button>
                    </span>
                  ))}
                </div>
              </div>
            ) : null}

            {activeSection === 'archive' ? (
              <div className="settings-section settings-section-flat">
                <h3>{t('archiveReasons')}</h3>
                <form className="settings-inline-form" onSubmit={(event) => void addArchiveReason(event)}>
                  <input
                    aria-label={t('addArchiveReason')}
                    onChange={(event) => setArchiveReasonName(event.target.value)}
                    placeholder={t('addArchiveReason')}
                    type="text"
                    value={archiveReasonName}
                  />
                  <label className="settings-checkbox">
                    <input
                      checked={archiveReasonRequiresNote}
                      onChange={(event) => setArchiveReasonRequiresNote(event.target.checked)}
                      type="checkbox"
                    />
                    {t('requireArchiveNote')}
                  </label>
                  <button className="icon-button" disabled={!archiveReasonName.trim()} type="submit">
                    <Icon name="plus" />
                  </button>
                </form>
                <div className="settings-list">
                  {archiveResolutions.map((reason) => (
                    <ArchiveResolutionSettingsRow
                      key={reason.id}
                      onDeleteArchiveResolution={onDeleteArchiveResolution}
                      onUpdateArchiveResolution={onUpdateArchiveResolution}
                      reason={reason}
                      t={t}
                    />
                  ))}
                </div>
              </div>
            ) : null}

            {activeSection === 'cleanup' ? (
              <div className="settings-section settings-section-flat">
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
            ) : null}
          </div>
        </div>
      </section>
    </ModalFrame>
  );
}

function ArchiveResolutionSettingsRow({
  onDeleteArchiveResolution,
  onUpdateArchiveResolution,
  reason,
  t,
}: {
  onDeleteArchiveResolution: (id: string) => Promise<void>;
  onUpdateArchiveResolution: (
    id: string,
    requestBody: UpdateArchiveResolutionRequest,
  ) => Promise<void>;
  reason: ArchiveResolutionResponse;
  t: Translate;
}) {
  const [name, setName] = useState(reason.name);
  const [requiresExplanation, setRequiresExplanation] = useState(reason.requiresExplanation);

  useEffect(() => {
    setName(reason.name);
    setRequiresExplanation(reason.requiresExplanation);
  }, [reason]);

  const saveReason = async () => {
    const trimmedName = name.trim();
    if (!trimmedName) {
      setName(reason.name);
      return;
    }

    await onUpdateArchiveResolution(reason.id, {
      name: trimmedName,
      requiresExplanation,
    });
  };

  return (
    <div className="settings-row">
      <input
        aria-label={reason.name}
        onBlur={() => void saveReason()}
        onChange={(event) => setName(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === 'Enter') {
            event.currentTarget.blur();
          }
        }}
        type="text"
        value={name}
      />
      <label className="settings-checkbox">
        <input
          checked={requiresExplanation}
          onChange={(event) => {
            setRequiresExplanation(event.target.checked);
            void onUpdateArchiveResolution(reason.id, {
              name: name.trim() || reason.name,
              requiresExplanation: event.target.checked,
            });
          }}
          type="checkbox"
        />
        {t('requireArchiveNote')}
      </label>
      <button
        className="tiny-icon-button danger-icon-button"
        onClick={() => void onDeleteArchiveResolution(reason.id)}
        title={t('deleteNote')}
        type="button"
      >
        <Icon name="trash" />
      </button>
    </div>
  );
}

function TaskMetaChip({
  icon,
  label,
  style,
  value,
}: {
  icon: IconName;
  label: string;
  style?: CSSProperties;
  value: string;
}) {
  return (
    <span className="task-meta-chip" style={style} title={`${label}: ${value}`}>
      <Icon name={icon} />
      {label}: {value}
    </span>
  );
}

function TaskBadges({
  taskItem,
  t,
}: {
  taskItem: TaskItemSummaryResponse;
  t: Translate;
}) {
  const badges = getTaskBadges(taskItem, t);

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

export default App;
