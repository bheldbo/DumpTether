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
  listArchiveResolutions,
  listIncomingTaskShares,
  listIncomingWorkspaceInvitations,
  listProjects,
  listSavedViews,
  listWorkspaceInvitations,
  listWorkspaceMembers,
  listTaskItems,
  listTaskTemplates,
  listWorkspaces,
  loginUser,
  logoutUser,
  reopenTaskItem,
  registerUser,
  removeWorkspaceMember,
  revokeTaskShare,
  revokeWorkspaceInvitation,
  setCurrentWorkspaceId,
  isTemporarySession,
  updateArchiveResolution,
  updateProject,
  updateTaskItem,
  updateTaskTimelineEntry,
  updateTaskTemplate,
  updateWorkspace,
  updateWorkspaceById,
} from './api';
import './App.css';
import { FieldEditorList, FieldValueList } from './fieldRenderers';
import { toFieldValueMap } from './fieldValues';
import { startLiveUpdates, type LiveUpdateMessage } from './liveUpdates';
import { type Language, type Translate, translate } from './localization';
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
  FieldDefinitionType,
  FieldValueMap,
  LoginUserRequest,
  ProjectResponse,
  RegisterUserRequest,
  RegisterUserResponse,
  SavedViewFollowUpFilter,
  SavedViewResponse,
  SavedViewSortField,
  TaskItemDetailResponse,
  TaskItemShareResponse,
  TaskItemSummaryResponse,
  TaskShareInboxResponse,
  TaskShareLinkResponse,
  TaskTemplateDetailResponse,
  WorkspaceInvitationInboxResponse,
  WorkspaceInvitationResponse,
  WorkspaceMemberResponse,
  UpdateArchiveResolutionRequest,
  UpdateProjectRequest,
  UpdateTaskItemRequest,
  UpdateWorkspaceRequest,
  UpsertFieldDefinitionRequest,
  WorkspaceResponse,
} from './types';

type WorkspaceMode = 'tasks' | 'templates';
type SettingsSectionKey = 'general' | 'statuses' | 'archive' | 'cleanup';

type IconName =
  | 'archive'
  | 'arrowDown'
  | 'arrowUp'
  | 'back'
  | 'check'
  | 'calendarX'
  | 'cloud'
  | 'clock'
  | 'close'
  | 'crown'
  | 'edit'
  | 'filterOff'
  | 'inbox'
  | 'list'
  | 'login'
  | 'logout'
  | 'mail'
  | 'note'
  | 'palette'
  | 'panel'
  | 'plus'
  | 'refresh'
  | 'search'
  | 'settings'
  | 'shield'
  | 'status'
  | 'tag'
  | 'templates'
  | 'trash'
  | 'undo'
  | 'user'
  | 'users'
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

interface TaskWallFilters {
  text: string;
  status: string;
  category: string;
  color: string;
  projectId: string;
  notTouchedDays: string;
  followUp: '' | SavedViewFollowUpFilter;
  sharedWith: string;
  sharedWithMe: boolean;
}

const fieldTypes: FieldDefinitionType[] = [
  'Text',
  'LongText',
  'Date',
  'Checkbox',
  'Select',
];

const followUpFilters: SavedViewFollowUpFilter[] = [
  'Any',
  'Overdue',
  'Today',
  'ThisWeek',
];
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
const statusOptionsStorageKey = 'dumptether.statusOptions';
const sidebarWidthStorageKey = 'dumptether.sidebarWidth';
const minSidebarWidth = 232;
const maxSidebarWidth = 440;
const defaultAuthOptions: AuthClientOptionsResponse = {
  requiresAuthentication: true,
  guestSessionsEnabled: true,
  developmentLoginEnabled: false,
  emailConfirmationEnabled: false,
  oAuthProviders: [],
};

type ConnectionStatus = 'checking' | 'online' | 'offline';
type ToastTone = 'info' | 'warning' | 'error';

interface ToastMessage {
  id: number;
  tone: ToastTone;
  message: string;
}

interface CachedWorkspaceSnapshot {
  archiveResolutions: ArchiveResolutionResponse[];
  currentViewId: string | null;
  knownStatuses: string[];
  projects: ProjectResponse[];
  savedViews: SavedViewResponse[];
  taskColorOptions: string[];
  taskItems: TaskItemSummaryResponse[];
  templates: TaskTemplateDetailResponse[];
  viewCounts: Record<string, number>;
  workspace: WorkspaceResponse | null;
  workspaceInvitations: WorkspaceInvitationResponse[];
  workspaceMembers: WorkspaceMemberResponse[];
  workspaces: WorkspaceResponse[];
}

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
  const liveConnectionToastAtRef = useRef(0);
  const [selectedWorkspaceId, setSelectedWorkspaceId] = useState<string | null>(
    getInitialWorkspaceId,
  );
  const [taskColorOptions, setTaskColorOptions] = useState<string[]>([]);
  const [isLoadingWorkspace, setIsLoadingWorkspace] = useState(true);
  const [isLoadingDetail, setIsLoadingDetail] = useState(false);
  const [isLoadingAuth, setIsLoadingAuth] = useState(true);
  const [hasBootstrapped, setHasBootstrapped] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
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
    const id = Date.now();
    setToasts((currentToasts) => [
      ...currentToasts.slice(-2),
      { id, message, tone },
    ]);
    window.setTimeout(() => {
      setToasts((currentToasts) => currentToasts.filter((toast) => toast.id !== id));
    }, 5200);
  }, []);

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

  const loadWorkspace = useCallback(
    async (
      preferredViewId: string | null = currentViewId,
      preferredWorkspaceId: string | null = selectedWorkspaceId,
      options: { force?: boolean } = {},
    ) => {
      setIsLoadingWorkspace(true);

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
            setWorkspaces(cachedSnapshot.workspaces);
            setWorkspace(cachedSnapshot.workspace);
            setSelectedWorkspaceId(cachedSnapshot.workspace?.id ?? preferredWorkspaceId);
            setSavedViews(cachedSnapshot.savedViews);
            setProjects(cachedSnapshot.projects);
            setArchiveResolutions(cachedSnapshot.archiveResolutions);
            setWorkspaceMembers(cachedSnapshot.workspaceMembers ?? []);
            setWorkspaceInvitations(cachedSnapshot.workspaceInvitations ?? []);
            setTemplates(cachedSnapshot.templates);
            setTaskColorOptions(cachedSnapshot.taskColorOptions);
            setKnownStatuses(cachedSnapshot.knownStatuses);
            setCurrentViewId(cachedSnapshot.currentViewId);
            setTaskItems(cachedSnapshot.taskItems);
            setViewCounts(cachedSnapshot.viewCounts);
            setIsLoadingWorkspace(false);
          }
        }

        setCurrentWorkspaceId(null);
        const workspaceList = await listWorkspaces();
        const effectiveWorkspaceId =
          preferredWorkspaceId && workspaceList.some((candidate) => candidate.id === preferredWorkspaceId)
            ? preferredWorkspaceId
            : workspaceList[0]?.id ?? null;

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
          getWorkspace(),
          listSavedViews(),
          listProjects(),
          listArchiveResolutions(),
          listTaskTemplates(),
          listWorkspaceMembers().catch(() => []),
          listWorkspaceInvitations().catch(() => []),
        ]);
        const resolvedWorkspaceList = workspaceList.some((candidate) => candidate.id === workspaceInfo.id)
          ? workspaceList
          : await listWorkspaces();
        const selectedViewId = pickSavedViewId(views, preferredViewId);
        const resolvedWorkspaceCacheKey = buildWorkspaceCacheKey(
          workspaceInfo.id,
          selectedViewId ?? 'default',
          cacheIdentity,
        );
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

        setWorkspaces(resolvedWorkspaceList);
        setWorkspace(workspaceInfo);
        setSelectedWorkspaceId(workspaceInfo.id);
        setSavedViews(views);
        setProjects(projectList);
        setArchiveResolutions(resolutions);
        setWorkspaceMembers(members);
        setWorkspaceInvitations(invitations);
        setTemplates(templateDetails);
        setTaskColorOptions(mergeColorOptions(getTaskColors(allTasksForColors)));
        setKnownStatuses(uniqueSorted(allTasksForColors.map((taskItem) => taskItem.status)));
        setCurrentViewId(selectedViewId);
        setTaskItems(selectedTasks);
        setViewCounts(Object.fromEntries(countEntries));
        const snapshot = {
          archiveResolutions: resolutions,
          currentViewId: selectedViewId,
          knownStatuses: uniqueSorted(allTasksForColors.map((taskItem) => taskItem.status)),
          projects: projectList,
          savedViews: views,
          taskColorOptions: mergeColorOptions(getTaskColors(allTasksForColors)),
          taskItems: selectedTasks,
          templates: templateDetails,
          viewCounts: Object.fromEntries(countEntries),
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
        setErrorMessage(getErrorMessage(error));
      } finally {
        setIsLoadingWorkspace(false);
      }
    },
    [currentUser, currentViewId, selectedTaskId, selectedWorkspaceId],
  );

  useEffect(() => {
    void loadAuth();
  }, [loadAuth]);

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
    if (!currentUser || temporarySessionIsActive) {
      return undefined;
    }

    let isDisposed = false;
    let reloadTimer: number | undefined;

    const scheduleWorkspaceReload = () => {
      if (reloadTimer) {
        window.clearTimeout(reloadTimer);
      }

      reloadTimer = window.setTimeout(() => {
        void loadWorkspace(currentViewId, selectedWorkspaceId, { force: true });
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
        void getTaskItem(selectedTaskId)
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

  const handleLeaveWorkspaceAccess = async (workspaceId: string) => {
    const workspaceToLeave = workspaces.find((candidate) => candidate.id === workspaceId);

    try {
      if (workspaceToLeave && isTaskShareWorkspace(workspaceToLeave)) {
        const sharesToLeave = incomingTaskShares.filter((share) => share.workspaceId === workspaceId);
        await Promise.all(sharesToLeave.map((share) => leaveTaskShare(share.shareId)));
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
      return created;
    } catch (error) {
      setErrorMessage(getErrorMessage(error));
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
        onOpenAccount={() => setAccountIsOpen(true)}
        onOpenSettings={() => setSettingsIsOpen(true)}
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
        {errorMessage ? (
          <div className="error-banner" role="alert">
            <strong>Something needs attention.</strong>
            <span>{errorMessage}</span>
          </div>
        ) : null}

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
            onUpdateTaskItems={handleUpdateTaskItems}
            onUpdateTaskItem={handleUpdateTaskItem}
            onUpdateTimelineEntry={handleUpdateTimelineEntry}
            onUpdateProject={handleUpdateProject}
            onUpdateWorkspace={handleUpdateWorkspace}
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
      <ToastStack toasts={toasts} />
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
  const workspaceInputRef = useRef<HTMLInputElement>(null);
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
          <span className={`nav-count${accountNotificationCount > 0 ? ' notification-badge' : ''}`}>
            {accountNotificationCount > 0 ? accountNotificationCount : language.toUpperCase()}
          </span>
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
    <div className="dialog-backdrop" role="presentation">
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
    </div>
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
  onRemoveWorkspaceMember,
  onRevokeTaskShare,
  onRevokeWorkspaceInvitation,
  onCloseTaskItem,
  onSelectTaskItem,
  onUpdateFieldValues,
  onUpdateProject,
  onUpdateTaskItems,
  onUpdateTaskItem,
  onUpdateTimelineEntry,
  onUpdateWorkspace,
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
  onAddTimelineEntry: (note: string) => Promise<void>;
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
  onRemoveWorkspaceMember: (userId: string) => Promise<void>;
  onRevokeTaskShare: (taskItemId: string, shareId: string) => Promise<void>;
  onRevokeWorkspaceInvitation: (id: string) => Promise<void>;
  onCloseTaskItem: () => void;
  onSelectTaskItem: (id: string) => void;
  onUpdateFieldValues: (fieldValues: FieldValueMap) => Promise<void>;
  onUpdateProject: (id: string, requestBody: UpdateProjectRequest) => Promise<void>;
  onUpdateTaskItems: (taskItemIds: string[], requestBody: UpdateTaskItemRequest) => Promise<void>;
  onUpdateTaskItem: (requestBody: UpdateTaskItemRequest) => Promise<void>;
  onUpdateTimelineEntry: (entryId: string, note: string) => Promise<void>;
  onUpdateWorkspace: (requestBody: UpdateWorkspaceRequest) => Promise<void>;
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
  const workspaceIsSharedAccess = isTaskShareWorkspace(workspace ?? { accessKind: 'Membership' }) ||
    Boolean(currentWorkspaceMember && !isOwnerRole(currentWorkspaceMember.role));
  const canManageSharing = currentUserOwnsWorkspace && !workspaceIsSharedAccess;
  const canCreateTask = currentView?.filter.archive !== 'Archived' &&
    !workspaceIsSharedAccess;
  const [filters, setFilters] = useState<TaskWallFilters>(emptyTaskWallFilters);
  const [pendingDeletedNoteIds, setPendingDeletedNoteIds] = useState<string[]>([]);
  const [editModeIsEnabled, setEditModeIsEnabled] = useState(false);
  const [selectedTaskIds, setSelectedTaskIds] = useState<string[]>([]);
  const [batchArchiveIsOpen, setBatchArchiveIsOpen] = useState(false);
  const [batchShareIsOpen, setBatchShareIsOpen] = useState(false);
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
    setBatchShareIsOpen(false);
  };

  const closeFocusedTask = useCallback(async () => {
    const idsToDelete = pendingDeletedNoteIds;

    setPendingDeletedNoteIds([]);

    for (const entryId of idsToDelete) {
      await onDeleteTimelineEntry(entryId);
    }

    onCloseTaskItem();
  }, [onCloseTaskItem, onDeleteTimelineEntry, pendingDeletedNoteIds]);

  const openCreateTask = useCallback(() => {
    if (!canCreateTask) {
      onShowToast(t('sharedBoardsCannotCreateTasks'), 'error');
      return;
    }

    if (focusedTaskItem || draftTaskIsOpen) {
      return;
    }

    setDraftTaskIsOpen(true);
  }, [canCreateTask, draftTaskIsOpen, focusedTaskItem, onShowToast, t]);

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
        if (visibleTaskItems.length > 0) {
          setEditModeIsEnabled((isEnabled) => !isEnabled);
          window.dispatchEvent(new CustomEvent('dumptether:open-actions'));
        }
      }
    };

    window.addEventListener('keydown', handleKeyDown);

    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [openCreateTask, visibleTaskItems.length]);

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
          colorOptions={colorOptions}
          invitations={workspaceInvitations}
          members={workspaceMembers}
          projects={projects}
          selectedProjectId={filters.projectId}
          t={t}
          workspace={workspace}
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
                  {taskCategoryNames.map((categoryName) => {
                    const categoryProject = projectByName.get(categoryName.toLowerCase()) ?? null;
                    return (
                      <TaskMetaChip
                        icon="tag"
                        key={categoryName}
                        label={t('category')}
                        style={getContextChipStyle(categoryProject?.color ?? null)}
                        value={categoryName}
                      />
                    );
                  })}
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
                    <span title={`${t('category')}: ${taskCategoryNames.join(', ')}`}>
                      {taskCategoryNames.join(', ')}
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
      {!isLoading && canCreateTask && !focusModeIsEnabled ? (
        <FloatingBoardActions
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
          onOpenBatchShare={() => setBatchShareIsOpen(true)}
          onToggleEditMode={() =>
            editModeIsEnabled ? closeEditMode() : setEditModeIsEnabled(true)}
          selectedTaskCount={selectedTaskIds.length}
          canManageSharing={canManageSharing}
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
          onCreate={async (email) =>
            await onCreateTaskShareLinks({
              email,
              taskItemIds: selectedTaskIds,
            })}
          onRevokeTaskShare={undefined}
          pendingInvitations={[]}
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
    </section>
  );
}

function WorkspaceHeader({
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
  projects,
  selectedProjectId,
  t,
  workspace,
}: {
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
              <h1 id="task-board-title">
                {workspace ? formatWorkspaceName(workspace.name, t) : 'DumpTether'}
              </h1>
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
            onCreate={async (email) => {
              const created = await onCreateWorkspaceInvitation({
                email,
                role: 2,
              });

              return {
                expiresAt: created.expiresAt,
                token: created.token ?? '',
              };
            }}
            onRevokeTaskShare={undefined}
            onRevokeWorkspaceInvitation={onRevokeWorkspaceInvitation}
            pendingInvitations={pendingInvitations}
            t={t}
            title={workspace ? formatWorkspaceName(workspace.name, t) : t('workspaces')}
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
          {newProjectIsOpen ? (
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
          ) : (
            <button
              className="project-tag project-tag-add"
              onClick={() => setNewProjectIsOpen(true)}
              title={t('newProjectTag')}
              type="button"
            >
              <Icon name="plus" />
              <span>{t('newProjectTag')}</span>
            </button>
          )}
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
    <div className="dialog-backdrop" role="presentation">
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
    </div>
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
  onRequestRemove,
  t,
}: {
  isConfirming: boolean;
  member: WorkspaceMemberResponse;
  onCancelRemove: () => void;
  onConfirmRemove: () => Promise<void>;
  onRequestRemove: () => void;
  t: Translate;
}) {
  const canRemove = !isOwnerRole(member.role);
  const isOwner = isOwnerRole(member.role);

  return (
    <span
      className={`member-chip member-chip-manageable${isOwner ? ' member-chip-owner' : ''}`}
      data-confirming={isConfirming}
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
  onClose,
  onCreate,
  onRevokeTaskShare,
  onRevokeWorkspaceInvitation,
  pendingInvitations,
  t,
  title,
}: {
  existingTaskShares: TaskItemShareResponse[];
  onClose: () => void;
  onCreate: (
    email: string,
  ) => Promise<{ token: string | null; expiresAt: string }>;
  onRevokeTaskShare?: (shareId: string) => Promise<void>;
  onRevokeWorkspaceInvitation?: (id: string) => Promise<void>;
  pendingInvitations: WorkspaceInvitationResponse[];
  t: Translate;
  title: string;
}) {
  const [shareEmail, setShareEmail] = useState('');
  const [createdLink, setCreatedLink] = useState<string | null>(null);
  const [copiedText, setCopiedText] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const visibleTaskShares = existingTaskShares.filter((share) => !share.revokedAt);

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
      const created = await onCreate(trimmedEmail);
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

  return (
    <div
      className="dialog-backdrop share-dialog-backdrop"
      onClick={(event) => {
        event.stopPropagation();
        if (event.target === event.currentTarget) {
          onClose();
        }
      }}
      onPointerDown={(event) => event.stopPropagation()}
      role="presentation"
    >
      <section
        aria-labelledby="share-dialog-title"
        aria-modal="true"
        className="workspace-invite-dialog share-dialog"
        onClick={(event) => event.stopPropagation()}
        onPointerDown={(event) => event.stopPropagation()}
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

        {pendingInvitations.length > 0 || visibleTaskShares.length > 0 ? (
          <div className="pending-invite-list share-dialog-list">
            {pendingInvitations.map((invitation) => (
              <span
                className="pending-invite-chip"
                key={invitation.id}
                title={`${invitation.email} - ${formatDateTime(invitation.expiresAt)}`}
              >
                <Icon name="mail" />
                <span>{invitation.email}</span>
                <small>{t('pendingInvites')}</small>
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
              <span
                className="share-chip"
                key={share.id}
                title={`${share.email}${share.expiresAt ? ` - ${formatDateTime(share.expiresAt)}` : ''}`}
              >
                <Icon name={share.acceptedAt ? 'user' : 'mail'} />
                <span>{share.email}</span>
                <small>{share.acceptedAt ? t('sharedWith') : t('pendingInvites')}</small>
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
              </span>
            ))}
          </div>
        ) : (
          <p className="context-muted share-dialog-empty">{t('notShared')}</p>
        )}
      </section>
    </div>
  );
}

function FloatingBoardActions({
  canManageSharing,
  colorOptions,
  editModeIsEnabled,
  onBatchUpdate,
  onCopyTaskItemsToWorkspace,
  onOpenCreateTask,
  onOpenBatchArchive,
  onOpenBatchShare,
  onToggleEditMode,
  projects,
  selectedTaskCount,
  statusOptions,
  taskCount,
  t,
  workspaces,
}: {
  canManageSharing: boolean;
  colorOptions: string[];
  editModeIsEnabled: boolean;
  onBatchUpdate: (requestBody: UpdateTaskItemRequest) => Promise<void>;
  onCopyTaskItemsToWorkspace: (workspaceId: string) => Promise<void>;
  onOpenCreateTask: () => void;
  onOpenBatchArchive: () => void;
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
    <div className="floating-board-actions" ref={menuRef}>
      <button
        className="quick-create-fab"
        data-active={isOpen}
        onClick={() => setIsOpen((open) => !open)}
        title={t('newTask')}
        type="button"
      >
        <Icon name="plus" />
        <span>{t('newTask')}</span>
      </button>

      {isOpen ? (
        <div className="quick-action-menu">
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
              {canManageSharing ? (
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
                  setIsOpen(true);
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
        onChange={(event) => updateFilter({ category: event.target.value, projectId: '' })}
        value={filters.category}
      >
        <option value="">{t('anyCategory')}</option>
        {projects.map((project) => (
          <option key={project.id} value={project.name}>
            {project.name}
          </option>
        ))}
        {options.categories
          .filter((category) => !projects.some((project) =>
            project.name.toLowerCase() === category.toLowerCase()))
          .map((category) => (
            <option key={category} value={category}>
              {category}
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

      <input
        aria-label={t('sharedWith')}
        onChange={(event) => updateFilter({ sharedWith: event.target.value })}
        placeholder={t('sharedWith')}
        type="search"
        value={filters.sharedWith}
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
  onAddTimelineEntry: (note: string) => Promise<void>;
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
  onUpdateTimelineEntry: (entryId: string, note: string) => Promise<void>;
  pendingDeletedNoteIds: string[];
  projects: ProjectResponse[];
  statusOptions: string[];
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
              event.target.closest('button, input, select, textarea, label, .color-popover, .task-share-popover, .share-dialog')
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
              t={t}
              taskItem={taskItem}
            />
          </div>
        ) : null}
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

function TaskShareStrip({
  onCreateTaskShareLink,
  onRevokeTaskShare,
  t,
  taskItem,
}: {
  onCreateTaskShareLink: (
    taskItemId: string,
    requestBody: CreateTaskShareRequest,
  ) => Promise<TaskShareLinkResponse>;
  onRevokeTaskShare: (taskItemId: string, shareId: string) => Promise<void>;
  t: Translate;
  taskItem: TaskItemDetailResponse;
}) {
  const [isOpen, setIsOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  return (
    <div
      className="task-share-popover"
      onClick={(event) => event.stopPropagation()}
      onPointerDown={(event) => event.stopPropagation()}
      ref={menuRef}
    >
      <button
        className="secondary-action task-share-trigger"
        onClick={(event) => {
          event.stopPropagation();
          setIsOpen((open) => !open);
        }}
        title={t('shareTask')}
        type="button"
      >
        <Icon name="users" />
        <span>{taskItem.shares.length > 0 ? taskItem.shares.length : t('shareTask')}</span>
      </button>

      {isOpen ? (
        <ShareDialog
          existingTaskShares={taskItem.shares}
          onClose={() => setIsOpen(false)}
          onCreate={async (email) =>
            await onCreateTaskShareLink(taskItem.id, {
              email,
              role: 2,
            })}
          onRevokeTaskShare={(shareId) => onRevokeTaskShare(taskItem.id, shareId)}
          pendingInvitations={[]}
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

  const saveChanges = async (overrides: Partial<{
    title: string;
    status: string;
    category: string;
    projectId: string | null;
    followUpDate: string;
  }> = {}, options: { keepEditing?: boolean } = {}) => {
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
        setEditingField(null);
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
        setEditingField(null);
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
          <select
            aria-label={t('status')}
            autoFocus
            disabled={isSubmitting}
            onBlur={() => void saveChanges()}
            onChange={(event) => {
              setStatus(event.target.value);
              void saveChanges({ status: event.target.value });
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
            onClick={() => setEditingField('status')}
            type="button"
          >
            {t('status')}: {taskItem.status ?? t('noStatus')}
          </button>
        )}
        {editingField === 'category' ? (
          <CategoryMultiSelect
            disabled={isSubmitting}
            onChange={(nextCategories) => {
              const nextCategory = joinTaskCategories(nextCategories) ?? '';
              const nextProjectId = getPrimaryProjectIdForCategories(nextCategory, projects);
              setCategory(nextCategory);
              setCategoryProjectId(nextProjectId ?? '');
              void saveChanges(
                {
                  category: nextCategory,
                  projectId: nextProjectId,
                },
                { keepEditing: true },
              );
            }}
            onClose={() => setEditingField(null)}
            projects={projects}
            selectedCategories={selectedCategoryNames}
            t={t}
          />
        ) : (
          <button
            className="task-meta-chip"
            onClick={() => setEditingField('category')}
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

function CategoryMultiSelect({
  disabled,
  onChange,
  onClose,
  projects,
  selectedCategories,
  t,
}: {
  disabled: boolean;
  onChange: (categories: string[]) => void;
  onClose: () => void;
  projects: ProjectResponse[];
  selectedCategories: string[];
  t: Translate;
}) {
  const selectedNames = new Set(selectedCategories.map((category) => category.toLowerCase()));

  const toggleCategory = (project: ProjectResponse) => {
    const hasCategory = selectedNames.has(project.name.toLowerCase());
    const nextCategories = hasCategory
      ? selectedCategories.filter((category) =>
        category.toLowerCase() !== project.name.toLowerCase())
      : [...selectedCategories, project.name];

    onChange(nextCategories);
  };

  return (
    <div className="category-multi-select" onClick={(event) => event.stopPropagation()}>
      <div className="category-option-list">
        {projects.length === 0 ? (
          <span className="context-muted">{t('noCategory')}</span>
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
      <div className="category-multi-actions">
        <button
          className="tiny-icon-button"
          disabled={disabled || selectedCategories.length === 0}
          onClick={() => onChange([])}
          title={t('noCategory')}
          type="button"
        >
          <Icon name="close" />
        </button>
        <button
          className="tiny-icon-button"
          onClick={onClose}
          title={t('saved')}
          type="button"
        >
          <Icon name="check" />
        </button>
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
    <div className="color-popover" data-placement={placement} ref={popoverRef}>
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
                onClick={() => setDraftColor(choice)}
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
              onClick={commitColor}
              title={t('saved')}
              type="button"
            >
              <Icon name="check" />
            </button>
            <button
              className="tiny-icon-button"
              onClick={cancelColor}
              title={t('cancel')}
              type="button"
            >
              <Icon name="close" />
            </button>
          </div>
          {color ? (
            <button
              className="clear-color-button"
              onClick={() => {
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
            className="icon-button note-undo-button"
            onClick={() => onUndoDeleteTimelineEntry(entry.id)}
            title={t('undo')}
            type="button"
          >
            <Icon name="undo" />
            <span className="sr-only">{t('undo')}</span>
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
              <Icon name="check" />
            </button>
            <button
              className="icon-button"
              onClick={() => setIsConfirmingDelete(false)}
              title={t('keep')}
              type="button"
            >
              <Icon name="close" />
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
  bodyText,
  onArchive,
  onClose,
  t,
  taskTitle,
}: {
  archiveResolutions: ArchiveResolutionResponse[];
  bodyText?: string;
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
          {bodyText ? <p className="resolution-description">{bodyText}</p> : null}
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

function ToastStack({ toasts }: { toasts: ToastMessage[] }) {
  if (toasts.length === 0) {
    return null;
  }

  return (
    <div className="toast-stack" role="status" aria-live="polite">
      {toasts.map((toast) => (
        <div className="toast" data-tone={toast.tone} key={toast.id}>
          {toast.message}
        </div>
      ))}
    </div>
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
    <div className="dialog-backdrop" role="presentation">
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
    </div>
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
    </div>
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

function formatWorkspaceRole(
  role: CurrentUserResponse['workspaces'][number]['role'],
  t: Translate,
) {
  return isOwnerRole(role) ? t('roleOwner') : t('roleMember');
}

function isOwnerRole(role: CurrentUserResponse['workspaces'][number]['role']) {
  return role === 1 || role === 'Owner';
}

function isTaskShareWorkspace(workspace: Pick<WorkspaceResponse, 'accessKind'>) {
  return workspace.accessKind === 'TaskShare';
}

function formatOAuthProvider(provider: string, t: Translate) {
  const normalizedProvider = provider.toLowerCase();
  const providerName = normalizedProvider === 'google'
    ? 'Google'
    : normalizedProvider === 'microsoft'
      ? 'Microsoft'
      : normalizedProvider === 'facebook'
        ? 'Facebook'
        : provider;

  return `${t('continueWith')} ${providerName}`;
}

function Icon({ name }: { name: IconName }) {
  const paths: Record<IconName, string> = {
    archive: 'M4 7h16v13H4V7Zm2-4h12l2 4H4l2-4Zm5 8h2',
    arrowDown: 'M12 5v14m0 0 6-6m-6 6-6-6',
    arrowUp: 'M12 19V5m0 0 6 6m-6-6-6 6',
    back: 'M15 6 9 12l6 6M10 12h10',
    calendarX: 'M7 3v4M17 3v4M4 9h16M6 5h12a2 2 0 0 1 2 2v11a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2Zm6 8 4 4m0-4-4 4',
    check: 'm5 13 4 4L19 7',
    cloud: 'M17 18H8a5 5 0 1 1 .9-9.9A6.5 6.5 0 0 1 21 11.5 3.5 3.5 0 0 1 17 18Z',
    clock: 'M12 4a8 8 0 1 0 0 16 8 8 0 0 0 0-16Zm0 4v5l3 2',
    close: 'M6 6l12 12M18 6 6 18',
    crown: 'M5 17h14l1-9-5 4-3-6-3 6-5-4 1 9Zm1 3h12',
    edit: 'M4 20h4l10-10-4-4L4 16v4Zm12-16 4 4',
    filterOff: 'M4 5h16l-6 7v3l-4 2v-5L4 5Zm3 15 13-13',
    inbox: 'M4 5h16v10l-3 4H7l-3-4V5Zm0 10h5l1.5 2h3L15 15h5',
    list: 'M8 6h12M8 12h12M8 18h12M4 6h.01M4 12h.01M4 18h.01',
    login: 'M10 17l5-5-5-5M15 12H3M21 5v14a2 2 0 0 1-2 2h-5M14 3h5a2 2 0 0 1 2 2',
    logout: 'M14 7l-5 5 5 5M9 12h12M3 5v14a2 2 0 0 0 2 2h5M10 3H5a2 2 0 0 0-2 2',
    mail: 'M4 6h16v12H4V6Zm0 2 8 5 8-5',
    note: 'M5 4h11l3 3v13H5V4Zm11 0v4h4M8 12h8M8 16h6',
    palette: 'M12 4a8 8 0 0 0-1 15.94c.8.1 1.33-.55 1.14-1.33-.13-.55.28-1.04.85-1.04h1.36A5.65 5.65 0 0 0 20 11.92C20 7.55 16.42 4 12 4ZM8 11.5h.01M10 8h.01M14 8h.01M16 11h.01',
    panel: 'M4 5h16v14H4V5Zm5 0v14',
    plus: 'M12 5v14M5 12h14',
    refresh: 'M20 7v5h-5M4 17v-5h5M18 10a6 6 0 0 0-10-4L4 10m2 4a6 6 0 0 0 10 4l4-4',
    search: 'M11 4a7 7 0 1 0 0 14 7 7 0 0 0 0-14Zm5 12 4 4',
    settings: 'M12 8.5a3.5 3.5 0 1 0 0 7 3.5 3.5 0 0 0 0-7Zm8 3.5-2.1-.6a6.9 6.9 0 0 0-.7-1.7l1.1-1.9-2.1-2.1-1.9 1.1a6.9 6.9 0 0 0-1.7-.7L12 4H9l-.6 2.1a6.9 6.9 0 0 0-1.7.7L4.8 5.7 2.7 7.8l1.1 1.9a6.9 6.9 0 0 0-.7 1.7L1 12l.6 3 2.1.6c.2.6.4 1.2.7 1.7l-1.1 1.9 2.1 2.1 1.9-1.1c.5.3 1.1.5 1.7.7L9 23h3l.6-2.1c.6-.2 1.2-.4 1.7-.7l1.9 1.1 2.1-2.1-1.1-1.9c.3-.5.5-1.1.7-1.7L20 15l.6-3Z',
    shield: 'M12 3 20 6v6c0 5-3.4 8-8 9-4.6-1-8-4-8-9V6l8-3Zm-3 9 2 2 4-5',
    status: 'M5 7h14M5 12h14M5 17h9',
    tag: 'M20 10 14 4H5v9l6 6 9-9ZM8 8h.01',
    templates: 'M4 5h7v7H4V5Zm9 0h7v7h-7V5ZM4 14h7v5H4v-5Zm9 0h7v5h-7v-5Z',
    trash: 'M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3',
    undo: 'M9 7H4v5m0 0 5-5m-5 5h10a6 6 0 1 1-4.2 10.2',
    user: 'M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8Zm-7 8a7 7 0 0 1 14 0',
    users: 'M9 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8Zm-6 8a7 7 0 0 1 12 0M17 11a3 3 0 1 0 0-6M15 20a5 5 0 0 1 7-4.5',
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

function readStoredStringList(key: string, fallback: string[]) {
  const storedValue = window.localStorage.getItem(key);

  if (!storedValue) {
    return fallback;
  }

  try {
    const parsed = JSON.parse(storedValue) as unknown;
    return Array.isArray(parsed)
      ? uniqueSorted(parsed.filter((value): value is string => typeof value === 'string'))
      : fallback;
  } catch {
    return fallback;
  }
}

function buildWorkspaceCacheKey(workspaceId: string, viewId: string, userId: string) {
  return `dumptether.cache.${userId}.${workspaceId}.${viewId}`;
}

function readCachedWorkspaceSnapshot(key: string): CachedWorkspaceSnapshot | null {
  const storedValue = window.sessionStorage.getItem(key);

  if (!storedValue) {
    return null;
  }

  try {
    const parsed = JSON.parse(storedValue) as CachedWorkspaceSnapshot & {
      cachedAt?: string;
    };
    return parsed;
  } catch {
    return null;
  }
}

function writeCachedWorkspaceSnapshot(key: string, snapshot: CachedWorkspaceSnapshot) {
  window.sessionStorage.setItem(
    key,
    JSON.stringify({
      ...snapshot,
      cachedAt: new Date().toISOString(),
    }),
  );
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
  sharedWith: '',
  sharedWithMe: false,
};

function buildTaskFilterOptions(
  taskItems: TaskItemSummaryResponse[],
  colorOptions: string[],
) {
  return {
    statuses: uniqueSorted(taskItems.map((taskItem) => taskItem.status)),
    categories: uniqueSorted(taskItems.flatMap((taskItem) => splitTaskCategories(taskItem.category))),
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

function splitTaskCategories(category: string | null | undefined) {
  if (!category) {
    return [];
  }

  return Array.from(
    new Set(
      category
        .split(';')
        .map((value) => value.trim())
        .filter(Boolean),
    ),
  );
}

function joinTaskCategories(categories: string[]) {
  const normalized = Array.from(
    new Set(
      categories
        .map((category) => category.trim())
        .filter(Boolean),
    ),
  );

  return normalized.length > 0 ? normalized.join('; ') : null;
}

function getProjectsForTaskCategories(
  category: string | null | undefined,
  projects: ProjectResponse[] | Map<string, ProjectResponse>,
) {
  const projectLookup = projects instanceof Map
    ? projects
    : new Map(projects.map((project) => [project.name.toLowerCase(), project]));

  return splitTaskCategories(category)
    .map((categoryName) => projectLookup.get(categoryName.toLowerCase()) ?? null)
    .filter((project): project is ProjectResponse => Boolean(project));
}

function getPrimaryProjectIdForCategories(
  category: string | null | undefined,
  projects: ProjectResponse[],
) {
  const firstProject = getProjectsForTaskCategories(category, projects)[0];
  return firstProject?.id ?? null;
}

function applyTaskWallFilters(
  taskItems: TaskItemSummaryResponse[],
  filters: TaskWallFilters,
  currentUserEmail: string | null,
  projects: ProjectResponse[],
) {
  const text = filters.text.trim().toLowerCase();
  const sharedWith = filters.sharedWith.trim().toLowerCase();
  const normalizedCurrentUserEmail = currentUserEmail?.trim().toLowerCase() ?? '';
  const notTouchedDays = numberOrNull(filters.notTouchedDays);

  return taskItems.filter((taskItem) => {
    if (text && !taskMatchesText(taskItem, text)) {
      return false;
    }

    if (filters.status && taskItem.status !== filters.status) {
      return false;
    }

    if (filters.category && !taskHasCategory(taskItem, filters.category)) {
      return false;
    }

    if (filters.color && taskItem.color !== filters.color) {
      return false;
    }

    if (filters.projectId && !taskMatchesProjectFilter(taskItem, filters.projectId, projects)) {
      return false;
    }

    if (filters.followUp && !taskMatchesFollowUp(taskItem, filters.followUp)) {
      return false;
    }

    if (notTouchedDays && !isNotTouchedForDays(taskItem, notTouchedDays)) {
      return false;
    }

    if (sharedWith &&
        !taskItem.shares.some((share) => share.email.toLowerCase().includes(sharedWith))) {
      return false;
    }

    if (filters.sharedWithMe &&
        !taskItem.shares.some((share) =>
          share.email.toLowerCase() === normalizedCurrentUserEmail)) {
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
    ...taskItem.shares.map((share) => share.email),
  ].some((value) => value?.toLowerCase().includes(text));
}

function taskMatchesProjectFilter(
  taskItem: TaskItemSummaryResponse,
  projectId: string,
  projects: ProjectResponse[],
) {
  if (taskItem.projectId === projectId) {
    return true;
  }

  const project = projects.find((candidate) => candidate.id === projectId);
  return project ? taskHasCategory(taskItem, project.name) : false;
}

function taskHasCategory(taskItem: TaskItemSummaryResponse, category: string) {
  return splitTaskCategories(taskItem.category).some((taskCategory) =>
    taskCategory.toLowerCase() === category.trim().toLowerCase());
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
  return Object.values(filters).some((value) =>
    typeof value === 'boolean' ? value : value.trim().length > 0,
  );
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

function getTaskBadges(taskItem: TaskItemSummaryResponse, t: Translate) {
  const badges: string[] = [];

  if (taskItem.archivedAt) {
    badges.push(t('archive'));
  }

  if (isWaiting(taskItem)) {
    badges.push(t('waiting'));
  }

  if (isStale(taskItem)) {
    badges.push(t('stale'));
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

function clamp(value: number, min: number, max: number) {
  return Math.min(max, Math.max(min, value));
}

function buildShareUrl(token: string) {
  const shareUrl = new URL(window.location.href);
  shareUrl.searchParams.set('shareToken', token);
  shareUrl.searchParams.delete('workspaceInvite');
  return shareUrl.toString();
}

async function copyTextToClipboard(value: string) {
  if (navigator.clipboard) {
    await navigator.clipboard.writeText(value);
    return;
  }

  const textarea = document.createElement('textarea');
  textarea.value = value;
  textarea.style.position = 'fixed';
  textarea.style.opacity = '0';
  document.body.append(textarea);
  textarea.select();
  document.execCommand('copy');
  textarea.remove();
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

function formatWorkspaceName(name: string, t: Translate) {
  return name.trim().toLowerCase() === 'all tasks'
    ? t('overview')
    : name;
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
