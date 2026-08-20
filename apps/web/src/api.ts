import type {
  AddTaskTimelineEntryRequest,
  AcceptShareLinkRequest,
  AcceptWorkspaceInvitationRequest,
  ArchiveProjectTasksRequest,
  ArchiveResolutionResponse,
  ArchiveTaskItemRequest,
  AccountDeletionResponse,
  AccountNotificationPreferencesResponse,
  AuthClientOptionsResponse,
  AuthSessionListItemResponse,
  CloudSyncAccountResponse,
  ConnectCloudAccountRequest,
  CurrentUserResponse,
  CreateArchiveResolutionRequest,
  CopyTaskItemsRequest,
  CopyTaskItemsResponse,
  DeleteTaskItemsRequest,
  DisconnectCloudAccountResponse,
  ForgotPasswordRequest,
  CreateTaskShareRequest,
  CreateTaskShareLinkRequest,
  CreateWorkspaceInvitationRequest,
  CreateProjectRequest,
  CreateSavedViewRequest,
  CreateTaskTemplateRequest,
  CreateTaskItemRequest,
  CreateWorkspaceRequest,
  ProjectResponse,
  ProjectArchiveResponse,
  LoginUserRequest,
  LoginUserResponse,
  LegalAcceptanceSubmission,
  RequestAccountDeletionRequest,
  ReconcileCloudWorkspacesResponse,
  ReopenTaskItemRequest,
  ReopenTaskItemsRequest,
  RegisterUserRequest,
  RegisterUserResponse,
  ResetPasswordRequest,
  SavedViewResponse,
  TaskTemplateDetailResponse,
  TaskTemplateImportResponse,
  TaskTemplateSummaryResponse,
  TaskItemListQuery,
  TaskItemDetailResponse,
  TaskItemBatchResponse,
  TaskItemSummaryResponse,
  TaskItemViewCountResponse,
  UpdateTaskItemRequest,
  UpdateTaskTimelineEntryRequest,
  UpdateArchiveResolutionRequest,
  UpdateAccountNotificationPreferencesRequest,
  UpdateSavedViewRequest,
  UpdateTaskTemplateRequest,
  UpdateProjectRequest,
  UpdateWorkspaceRequest,
  TaskShareInboxResponse,
  WorkspaceInvitationResponse,
  WorkspaceInvitationInboxResponse,
  WorkspaceMemberResponse,
  WorkspaceResponse,
  TaskItemShareResponse,
  TaskShareLinkResponse,
  ShareLinkAcceptResponse,
  SyncWorkspaceWithCloudRequest,
  SyncWorkspaceWithCloudResponse,
  SyncRootResponse,
  UpdateTaskShareRequest,
  UpdateWorkspaceMemberRequest,
} from './types';
import {
  getDesktopRuntimeConfiguration,
  isDesktopRuntime,
} from './clientRuntime';

export { isDesktopRuntime } from './clientRuntime';

const desktopRuntimeConfiguration = getDesktopRuntimeConfiguration();
const desktopLocalApiBaseUrl =
  desktopRuntimeConfiguration?.apiBaseUrl ?? 'http://127.0.0.1:55869';
const configuredBaseUrlValue = import.meta.env.VITE_API_BASE_URL?.trim().replace(/\/$/, '');
const configuredBaseUrl = configuredBaseUrlValue || undefined;
const apiBaseUrl = configuredBaseUrl ?? getDefaultApiBaseUrl();
const sessionTokenStorageKey = 'dumptether.sessionToken';
const guestSessionTokenStorageKey = 'dumptether.guestSessionToken';
const csrfCookieName = 'DumpTether.Csrf';
const csrfHeaderName = 'X-DumpTether-CSRF';
let currentWorkspaceId: string | null = null;
let currentSessionToken: string | null = readStoredSessionToken();
let sessionIsTemporary = readStoredTemporarySessionFlag();

interface ApiRequestOptions {
  workspaceId?: string | null;
  signal?: AbortSignal;
}

export function setCurrentWorkspaceId(workspaceId: string | null) {
  currentWorkspaceId = workspaceId;
}

export function getStoredSessionToken() {
  return currentSessionToken;
}

export function getApiBaseUrl() {
  return apiBaseUrl;
}

export function getCookieAuthCsrfHeader(): Record<string, string> {
  const csrfToken = readCookie(csrfCookieName);
  return csrfToken ? { [csrfHeaderName]: csrfToken } : {};
}

export function getDesktopBootstrapHeaders(): Record<string, string> {
  const bootstrapToken = desktopRuntimeConfiguration?.bootstrapToken;
  return bootstrapToken
    ? { 'X-DumpTether-Desktop-Bootstrap': bootstrapToken }
    : {};
}

function getDefaultApiBaseUrl() {
  if (typeof window === 'undefined') {
    return '';
  }

  if (isDesktopRuntime()) {
    return desktopLocalApiBaseUrl;
  }

  return '';
}

export function isTemporarySession() {
  return sessionIsTemporary;
}

export function setSessionToken(sessionToken: string | null, options: { temporary?: boolean } = {}) {
  currentSessionToken = sessionToken;
  sessionIsTemporary = Boolean(sessionToken && options.temporary);

  if (sessionToken && options.temporary) {
    window.sessionStorage.setItem(guestSessionTokenStorageKey, sessionToken);
    window.localStorage.removeItem(sessionTokenStorageKey);
  } else if (sessionToken) {
    window.localStorage.setItem(sessionTokenStorageKey, sessionToken);
    window.sessionStorage.removeItem(guestSessionTokenStorageKey);
  } else {
    window.localStorage.removeItem(sessionTokenStorageKey);
    window.sessionStorage.removeItem(guestSessionTokenStorageKey);
  }
}

export class ApiError extends Error {
  public readonly status: number;

  public constructor(message: string, status: number) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
  }
}

async function request<T>(
  path: string,
  init?: RequestInit,
  options: ApiRequestOptions = {},
): Promise<T> {
  const requestWorkspaceId = options.workspaceId === undefined
    ? currentWorkspaceId
    : options.workspaceId;
  const method = init?.method?.toUpperCase() ?? 'GET';
  const csrfToken = !currentSessionToken && requiresCsrfHeader(method)
    ? readCookie(csrfCookieName)
    : null;
  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    credentials: 'include',
    signal: init?.signal ?? options.signal,
    headers: {
      ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
      ...getDesktopBootstrapHeaders(),
      ...(currentSessionToken ? { Authorization: `Bearer ${currentSessionToken}` } : {}),
      ...(csrfToken ? { [csrfHeaderName]: csrfToken } : {}),
      ...(requestWorkspaceId ? { 'X-DumpTether-Workspace-Id': requestWorkspaceId } : {}),
      ...init?.headers,
    },
  });

  if (!response.ok) {
    let message = `Request failed with status ${response.status}.`;
    const text = await response.text();

    try {
      const body = JSON.parse(text) as { error?: string; title?: string };
      message = body.error ?? body.title ?? message;
    } catch {
      message = text || message;
    }

    throw new ApiError(message, response.status);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  const responseText = await response.text();
  return responseText ? JSON.parse(responseText) as T : undefined as T;
}

function readStoredSessionToken() {
  return window.localStorage.getItem(sessionTokenStorageKey) ??
    window.sessionStorage.getItem(guestSessionTokenStorageKey);
}

function readStoredTemporarySessionFlag() {
  return !window.localStorage.getItem(sessionTokenStorageKey) &&
    Boolean(window.sessionStorage.getItem(guestSessionTokenStorageKey));
}

function requiresCsrfHeader(method: string) {
  return !['GET', 'HEAD', 'OPTIONS', 'TRACE'].includes(method);
}

function readCookie(name: string) {
  if (typeof document === 'undefined') {
    return null;
  }

  const prefix = `${name}=`;
  const cookie = document.cookie
    .split(';')
    .map((part) => part.trim())
    .find((part) => part.startsWith(prefix));

  return cookie ? decodeURIComponent(cookie.slice(prefix.length)) : null;
}

export function registerUser(
  requestBody: RegisterUserRequest,
): Promise<RegisterUserResponse> {
  return request<RegisterUserResponse>('/api/auth/register', {
    method: 'POST',
    body: JSON.stringify(requestBody),
  });
}

export async function loginUser(
  requestBody: LoginUserRequest,
): Promise<LoginUserResponse> {
  const response = await request<LoginUserResponse>('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify(requestBody),
  });

  setSessionToken(response.sessionToken);
  return response;
}

export async function logoutUser(): Promise<void> {
  try {
    await request<void>('/api/auth/logout', {
      method: 'POST',
    });
  } catch (error) {
    if (!(error instanceof ApiError && error.status === 401)) {
      throw error;
    }
  } finally {
    setSessionToken(null);
  }
}

export function getCurrentUser(): Promise<CurrentUserResponse> {
  return request<CurrentUserResponse>('/api/auth/me');
}

export function listAuthSessions(): Promise<AuthSessionListItemResponse[]> {
  return request<AuthSessionListItemResponse[]>('/api/auth/sessions');
}

export function revokeAuthSession(sessionId: string): Promise<void> {
  return request<void>(`/api/auth/sessions/${sessionId}`, {
    method: 'DELETE',
  });
}

export function getAuthOptions(): Promise<AuthClientOptionsResponse> {
  return request<AuthClientOptionsResponse>('/api/auth/options');
}

export function forgotPassword(requestBody: ForgotPasswordRequest): Promise<void> {
  return request<void>('/api/auth/forgot-password', {
    method: 'POST',
    body: JSON.stringify(requestBody),
  });
}

export function resetPassword(requestBody: ResetPasswordRequest): Promise<void> {
  return request<void>('/api/auth/reset-password', {
    method: 'POST',
    body: JSON.stringify(requestBody),
  });
}

export async function getAccountDeletion(): Promise<AccountDeletionResponse | null> {
  return (await request<AccountDeletionResponse | null | undefined>(
    '/api/account/deletion',
  )) ?? null;
}

export function requestAccountDeletion(
  requestBody: RequestAccountDeletionRequest,
): Promise<void> {
  return request<void>('/api/account/deletion', {
    method: 'POST',
    body: JSON.stringify(requestBody),
  });
}

export function cancelAccountDeletion(): Promise<void> {
  return request<void>('/api/account/deletion', {
    method: 'DELETE',
  });
}

export function getAccountNotificationPreferences(): Promise<AccountNotificationPreferencesResponse> {
  return request<AccountNotificationPreferencesResponse>('/api/account/notifications');
}

export function updateAccountNotificationPreferences(
  requestBody: UpdateAccountNotificationPreferencesRequest,
): Promise<AccountNotificationPreferencesResponse> {
  return request<AccountNotificationPreferencesResponse>('/api/account/notifications', {
    method: 'PUT',
    body: JSON.stringify(requestBody),
  });
}

export function beginOAuthLogin(
  provider: string,
  legalAcceptance?: LegalAcceptanceSubmission | null,
) {
  const query = new URLSearchParams({ returnUrl: window.location.href });

  if (legalAcceptance) {
    query.set('termsAccepted', String(legalAcceptance.termsAccepted));
    query.set('termsVersion', legalAcceptance.termsVersion);
    query.set(
      'privacyNoticeAcknowledged',
      String(legalAcceptance.privacyNoticeAcknowledged),
    );
    query.set('privacyNoticeVersion', legalAcceptance.privacyNoticeVersion);
  }

  window.location.assign(`${apiBaseUrl}/api/auth/oauth/${provider}?${query.toString()}`);
}

export async function developmentLogin(): Promise<LoginUserResponse> {
  const response = await request<LoginUserResponse>('/api/auth/development-login', {
    method: 'POST',
  });

  setSessionToken(response.sessionToken);
  return response;
}

export async function guestLogin(): Promise<LoginUserResponse> {
  const response = await request<LoginUserResponse>('/api/auth/guest', {
    method: 'POST',
  });

  setSessionToken(response.sessionToken, { temporary: true });
  return response;
}

export async function localDesktopLogin(): Promise<LoginUserResponse> {
  const response = await request<LoginUserResponse>('/api/auth/local-desktop', {
    method: 'POST',
  });

  setSessionToken(response.sessionToken);
  return response;
}

export function checkHealth(): Promise<{ status: string; service: string }> {
  return request<{ status: string; service: string }>('/health');
}

export function listTaskItems(
  query: TaskItemListQuery = {},
  options: ApiRequestOptions = {},
): Promise<TaskItemSummaryResponse[]> {
  const searchParams = new URLSearchParams();

  Object.entries(query).forEach(([key, value]) => {
    if (value !== undefined && value !== null && value !== '') {
      searchParams.set(key, String(value));
    }
  });

  const queryString = searchParams.toString();
  return request<TaskItemSummaryResponse[]>(
    `/api/tasks${queryString ? `?${queryString}` : ''}`,
    undefined,
    options,
  );
}

export function listTaskViewCounts(
  viewIds: string[],
  options: ApiRequestOptions = {},
): Promise<TaskItemViewCountResponse[]> {
  const searchParams = new URLSearchParams();

  viewIds
    .filter(Boolean)
    .forEach((viewId) => searchParams.append('viewIds', viewId));

  const queryString = searchParams.toString();
  return request<TaskItemViewCountResponse[]>(
    `/api/tasks/view-counts${queryString ? `?${queryString}` : ''}`,
    undefined,
    options,
  );
}

export function getTaskItem(
  id: string,
  options: ApiRequestOptions = {},
): Promise<TaskItemDetailResponse> {
  return request<TaskItemDetailResponse>(`/api/tasks/${id}`, undefined, options);
}

export function listSubtasks(
  parentTaskItemId: string,
  options: ApiRequestOptions = {},
): Promise<TaskItemSummaryResponse[]> {
  return request<TaskItemSummaryResponse[]>(
    `/api/tasks/${parentTaskItemId}/subtasks`,
    undefined,
    options,
  );
}

export function createSubtask(
  parentTaskItemId: string,
  requestBody: CreateTaskItemRequest,
  options: ApiRequestOptions = {},
): Promise<TaskItemDetailResponse> {
  return request<TaskItemDetailResponse>(`/api/tasks/${parentTaskItemId}/subtasks`, {
    method: 'POST',
    body: JSON.stringify(requestBody),
  }, options);
}

export function createTaskItem(
  requestBody: CreateTaskItemRequest,
  options: ApiRequestOptions = {},
): Promise<TaskItemDetailResponse> {
  return request<TaskItemDetailResponse>('/api/tasks', {
    method: 'POST',
    body: JSON.stringify(requestBody),
  }, options);
}

export function copyTaskItems(
  requestBody: CopyTaskItemsRequest,
): Promise<CopyTaskItemsResponse> {
  return request<CopyTaskItemsResponse>('/api/tasks/copy', {
    method: 'POST',
    body: JSON.stringify(requestBody),
  });
}

export function updateTaskItem(
  id: string,
  requestBody: UpdateTaskItemRequest,
  options: ApiRequestOptions = {},
): Promise<TaskItemDetailResponse> {
  return request<TaskItemDetailResponse>(`/api/tasks/${id}`, {
    method: 'PATCH',
    body: JSON.stringify(requestBody),
  }, options);
}

export function addTaskTimelineEntry(
  id: string,
  requestBody: AddTaskTimelineEntryRequest,
  options: ApiRequestOptions = {},
): Promise<TaskItemDetailResponse> {
  return request<TaskItemDetailResponse>(`/api/tasks/${id}/timeline`, {
    method: 'POST',
    body: JSON.stringify(requestBody),
  }, options);
}

export function archiveTaskItem(
  id: string,
  requestBody: ArchiveTaskItemRequest,
  options: ApiRequestOptions = {},
): Promise<TaskItemDetailResponse> {
  return request<TaskItemDetailResponse>(`/api/tasks/${id}/archive`, {
    method: 'POST',
    body: JSON.stringify(requestBody),
  }, options);
}

export function reopenTaskItem(
  id: string,
  requestBody: ReopenTaskItemRequest,
  options: ApiRequestOptions = {},
): Promise<TaskItemDetailResponse> {
  return request<TaskItemDetailResponse>(`/api/tasks/${id}/reopen`, {
    method: 'POST',
    body: JSON.stringify(requestBody),
  }, options);
}

export function reopenTaskItems(
  requestBody: ReopenTaskItemsRequest,
): Promise<TaskItemBatchResponse> {
  return request<TaskItemBatchResponse>('/api/tasks/reopen', {
    method: 'POST',
    body: JSON.stringify(requestBody),
  });
}

export function deleteTaskItemsPermanently(
  requestBody: DeleteTaskItemsRequest,
  options: ApiRequestOptions = {},
): Promise<TaskItemBatchResponse> {
  return request<TaskItemBatchResponse>('/api/tasks/permanent-delete', {
    method: 'POST',
    body: JSON.stringify(requestBody),
  }, options);
}

export function listArchiveResolutions(
  options: ApiRequestOptions = {},
): Promise<ArchiveResolutionResponse[]> {
  return request<ArchiveResolutionResponse[]>('/api/archive-resolutions', undefined, options);
}

export function createArchiveResolution(
  requestBody: CreateArchiveResolutionRequest,
): Promise<ArchiveResolutionResponse> {
  return request<ArchiveResolutionResponse>('/api/archive-resolutions', {
    method: 'POST',
    body: JSON.stringify(requestBody),
  });
}

export function updateArchiveResolution(
  id: string,
  requestBody: UpdateArchiveResolutionRequest,
): Promise<ArchiveResolutionResponse> {
  return request<ArchiveResolutionResponse>(`/api/archive-resolutions/${id}`, {
    method: 'PATCH',
    body: JSON.stringify(requestBody),
  });
}

export function deleteArchiveResolution(id: string): Promise<void> {
  return request<void>(`/api/archive-resolutions/${id}`, {
    method: 'DELETE',
  });
}

export function listProjects(options: ApiRequestOptions = {}): Promise<ProjectResponse[]> {
  return request<ProjectResponse[]>('/api/projects', undefined, options);
}

export function createProject(
  requestBody: CreateProjectRequest,
): Promise<ProjectResponse> {
  return request<ProjectResponse>('/api/projects', {
    method: 'POST',
    body: JSON.stringify(requestBody),
  });
}

export function updateProject(
  id: string,
  requestBody: UpdateProjectRequest,
): Promise<ProjectResponse> {
  return request<ProjectResponse>(`/api/projects/${id}`, {
    method: 'PATCH',
    body: JSON.stringify(requestBody),
  });
}

export function archiveProjectTasks(
  id: string,
  requestBody: ArchiveProjectTasksRequest,
): Promise<ProjectArchiveResponse> {
  return request<ProjectArchiveResponse>(`/api/projects/${id}/archive-tasks`, {
    method: 'POST',
    body: JSON.stringify(requestBody),
  });
}

export function deleteProject(id: string): Promise<ProjectArchiveResponse> {
  return request<ProjectArchiveResponse>(`/api/projects/${id}`, {
    method: 'DELETE',
  });
}

export function getWorkspace(options: ApiRequestOptions = {}): Promise<WorkspaceResponse> {
  return request<WorkspaceResponse>('/api/workspace', undefined, options);
}

export function listWorkspaces(options: ApiRequestOptions = {}): Promise<WorkspaceResponse[]> {
  return request<WorkspaceResponse[]>('/api/workspaces', undefined, options);
}

export function createWorkspace(
  requestBody: CreateWorkspaceRequest,
): Promise<WorkspaceResponse> {
  return request<WorkspaceResponse>('/api/workspaces', {
    method: 'POST',
    body: JSON.stringify(requestBody),
  });
}

export function updateWorkspace(
  requestBody: UpdateWorkspaceRequest,
): Promise<WorkspaceResponse> {
  return request<WorkspaceResponse>('/api/workspace', {
    method: 'PATCH',
    body: JSON.stringify(requestBody),
  });
}

export function updateWorkspaceById(
  id: string,
  requestBody: UpdateWorkspaceRequest,
): Promise<WorkspaceResponse> {
  return request<WorkspaceResponse>(`/api/workspaces/${id}`, {
    method: 'PATCH',
    body: JSON.stringify(requestBody),
  });
}

export function deleteWorkspace(id: string): Promise<void> {
  return request<void>(`/api/workspaces/${id}`, {
    method: 'DELETE',
  });
}

export function listWorkspaceMembers(
  options: ApiRequestOptions = {},
): Promise<WorkspaceMemberResponse[]> {
  return request<WorkspaceMemberResponse[]>('/api/workspace/members', undefined, options);
}

export function listWorkspaceInvitations(
  options: ApiRequestOptions = {},
): Promise<WorkspaceInvitationResponse[]> {
  return request<WorkspaceInvitationResponse[]>('/api/workspace/invitations', undefined, options);
}

export function createWorkspaceInvitation(
  requestBody: CreateWorkspaceInvitationRequest,
): Promise<WorkspaceInvitationResponse> {
  return request<WorkspaceInvitationResponse>('/api/workspace/invitations', {
    method: 'POST',
    body: JSON.stringify(requestBody),
  });
}

export function acceptWorkspaceInvitation(
  requestBody: AcceptWorkspaceInvitationRequest,
): Promise<WorkspaceInvitationResponse> {
  return request<WorkspaceInvitationResponse>('/api/workspace/invitations/accept', {
    method: 'POST',
    body: JSON.stringify(requestBody),
  });
}

export function acceptShareLink(
  requestBody: AcceptShareLinkRequest,
): Promise<ShareLinkAcceptResponse> {
  return request<ShareLinkAcceptResponse>('/api/share-links/accept', {
    method: 'POST',
    body: JSON.stringify(requestBody),
  });
}

export function listIncomingWorkspaceInvitations(): Promise<WorkspaceInvitationInboxResponse[]> {
  return request<WorkspaceInvitationInboxResponse[]>('/api/account/invitations');
}

export function acceptIncomingWorkspaceInvitation(
  invitationId: string,
): Promise<WorkspaceInvitationResponse> {
  return request<WorkspaceInvitationResponse>(`/api/account/invitations/${invitationId}/accept`, {
    method: 'POST',
  });
}

export function declineIncomingWorkspaceInvitation(id: string): Promise<void> {
  return request<void>(`/api/account/invitations/${id}`, {
    method: 'DELETE',
  });
}

export function leaveCurrentWorkspace(): Promise<void> {
  return request<void>('/api/workspace/membership', {
    method: 'DELETE',
  });
}

export function removeWorkspaceMember(userId: string): Promise<void> {
  return request<void>(`/api/workspace/members/${userId}`, {
    method: 'DELETE',
  });
}

export function updateWorkspaceMemberRole(
  userId: string,
  requestBody: UpdateWorkspaceMemberRequest,
): Promise<WorkspaceMemberResponse> {
  return request<WorkspaceMemberResponse>(`/api/workspace/members/${userId}`, {
    method: 'PATCH',
    body: JSON.stringify(requestBody),
  });
}

export function revokeWorkspaceInvitation(id: string): Promise<void> {
  return request<void>(`/api/workspace/invitations/${id}`, {
    method: 'DELETE',
  });
}

export function listSavedViews(options: ApiRequestOptions = {}): Promise<SavedViewResponse[]> {
  return request<SavedViewResponse[]>('/api/views', undefined, options);
}

export function getSavedView(id: string): Promise<SavedViewResponse> {
  return request<SavedViewResponse>(`/api/views/${id}`);
}

export function createSavedView(
  requestBody: CreateSavedViewRequest,
): Promise<SavedViewResponse> {
  return request<SavedViewResponse>('/api/views', {
    method: 'POST',
    body: JSON.stringify(requestBody),
  });
}

export function updateTaskTimelineEntry(
  taskItemId: string,
  entryId: string,
  requestBody: UpdateTaskTimelineEntryRequest,
  options: ApiRequestOptions = {},
): Promise<TaskItemDetailResponse> {
  return request<TaskItemDetailResponse>(
    `/api/tasks/${taskItemId}/timeline/${entryId}`,
    {
      method: 'PATCH',
      body: JSON.stringify(requestBody),
    },
    options,
  );
}

export function deleteTaskTimelineEntry(
  taskItemId: string,
  entryId: string,
  options: ApiRequestOptions = {},
): Promise<TaskItemDetailResponse> {
  return request<TaskItemDetailResponse>(
    `/api/tasks/${taskItemId}/timeline/${entryId}`,
    {
      method: 'DELETE',
    },
    options,
  );
}

export function listTaskShares(id: string): Promise<TaskItemShareResponse[]> {
  return request<TaskItemShareResponse[]>(`/api/tasks/${id}/shares`);
}

export function createTaskShare(
  id: string,
  requestBody: CreateTaskShareRequest,
): Promise<TaskItemDetailResponse> {
  return request<TaskItemDetailResponse>(`/api/tasks/${id}/shares`, {
    method: 'POST',
    body: JSON.stringify(requestBody),
  });
}

export function createTaskShareLink(
  id: string,
  requestBody: CreateTaskShareRequest,
  options: ApiRequestOptions = {},
): Promise<TaskShareLinkResponse> {
  return request<TaskShareLinkResponse>(`/api/tasks/${id}/share-links`, {
    method: 'POST',
    body: JSON.stringify(requestBody),
  }, options);
}

export function createTaskShareLinks(
  requestBody: CreateTaskShareLinkRequest,
): Promise<TaskShareLinkResponse> {
  return request<TaskShareLinkResponse>('/api/tasks/share-links', {
    method: 'POST',
    body: JSON.stringify(requestBody),
  });
}

export function revokeTaskShare(
  id: string,
  shareId: string,
  options: ApiRequestOptions = {},
): Promise<TaskItemDetailResponse> {
  return request<TaskItemDetailResponse>(`/api/tasks/${id}/shares/${shareId}`, {
    method: 'DELETE',
  }, options);
}

export function updateTaskShareRole(
  id: string,
  shareId: string,
  requestBody: UpdateTaskShareRequest,
  options: ApiRequestOptions = {},
): Promise<TaskItemDetailResponse> {
  return request<TaskItemDetailResponse>(`/api/tasks/${id}/shares/${shareId}`, {
    method: 'PATCH',
    body: JSON.stringify(requestBody),
  }, options);
}

export function listIncomingTaskShares(): Promise<TaskShareInboxResponse[]> {
  return request<TaskShareInboxResponse[]>('/api/account/task-shares');
}

export function leaveTaskShare(shareId: string): Promise<void> {
  return request<void>(`/api/account/task-shares/${shareId}`, {
    method: 'DELETE',
  });
}

export function leaveWorkspaceTaskShares(workspaceId: string): Promise<void> {
  return request<void>(`/api/account/workspaces/${workspaceId}/task-shares`, {
    method: 'DELETE',
  });
}

export function updateSavedView(
  id: string,
  requestBody: UpdateSavedViewRequest,
): Promise<SavedViewResponse> {
  return request<SavedViewResponse>(`/api/views/${id}`, {
    method: 'PATCH',
    body: JSON.stringify(requestBody),
  });
}

export function deleteSavedView(id: string): Promise<void> {
  return request<void>(`/api/views/${id}`, {
    method: 'DELETE',
  });
}

export function listTaskTemplates(
  options: ApiRequestOptions = {},
): Promise<TaskTemplateSummaryResponse[]> {
  return request<TaskTemplateSummaryResponse[]>('/api/templates', undefined, options);
}

export function getTaskTemplate(
  id: string,
  options: ApiRequestOptions = {},
): Promise<TaskTemplateDetailResponse> {
  return request<TaskTemplateDetailResponse>(`/api/templates/${id}`, undefined, options);
}

export function createTaskTemplate(
  requestBody: CreateTaskTemplateRequest,
): Promise<TaskTemplateDetailResponse> {
  return request<TaskTemplateDetailResponse>('/api/templates', {
    method: 'POST',
    body: JSON.stringify(requestBody),
  });
}

export function updateTaskTemplate(
  id: string,
  requestBody: UpdateTaskTemplateRequest,
): Promise<TaskTemplateDetailResponse> {
  return request<TaskTemplateDetailResponse>(`/api/templates/${id}`, {
    method: 'PATCH',
    body: JSON.stringify(requestBody),
  });
}

export function deleteTaskTemplate(id: string): Promise<void> {
  return request<void>(`/api/templates/${id}`, {
    method: 'DELETE',
  });
}

export function importTaskTemplateFromTask(
  taskItemId: string,
  options: ApiRequestOptions = {},
): Promise<TaskTemplateImportResponse> {
  return request<TaskTemplateImportResponse>(`/api/tasks/${taskItemId}/template/import`, {
    method: 'POST',
  }, options);
}

export function syncWorkspaceWithCloud(
  workspaceId: string,
  requestBody: SyncWorkspaceWithCloudRequest,
): Promise<SyncWorkspaceWithCloudResponse> {
  return request<SyncWorkspaceWithCloudResponse>(`/api/sync/workspaces/${workspaceId}/run`, {
    method: 'POST',
    body: JSON.stringify(requestBody),
  }, { workspaceId });
}

export function getCloudSyncAccount(
  options: ApiRequestOptions = {},
): Promise<CloudSyncAccountResponse | null> {
  return request<CloudSyncAccountResponse | null>('/api/sync/cloud-account', undefined, options);
}

export function connectCloudSyncAccount(
  requestBody: ConnectCloudAccountRequest,
  options: ApiRequestOptions = {},
): Promise<CloudSyncAccountResponse> {
  return request<CloudSyncAccountResponse>('/api/sync/cloud-account', {
    method: 'POST',
    body: JSON.stringify(requestBody),
  }, options);
}

export function disconnectCloudSyncAccount(
  options: ApiRequestOptions = {},
): Promise<DisconnectCloudAccountResponse> {
  return request<DisconnectCloudAccountResponse>('/api/sync/cloud-account', {
    method: 'DELETE',
  }, options);
}

export function reconcileCloudWorkspaces(
  options: ApiRequestOptions = {},
): Promise<ReconcileCloudWorkspacesResponse> {
  return request<ReconcileCloudWorkspacesResponse>(
    '/api/sync/cloud-workspaces/reconcile',
    { method: 'POST' },
    options,
  );
}

export function listWorkspaceSyncRoots(
  options: ApiRequestOptions = {},
): Promise<SyncRootResponse[]> {
  return request<SyncRootResponse[]>('/api/sync/workspace-roots', undefined, options);
}
