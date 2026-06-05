import type {
  AddTaskTimelineEntryRequest,
  AcceptShareLinkRequest,
  AcceptWorkspaceInvitationRequest,
  ArchiveProjectTasksRequest,
  ArchiveResolutionResponse,
  ArchiveTaskItemRequest,
  AuthClientOptionsResponse,
  CurrentUserResponse,
  CreateArchiveResolutionRequest,
  CopyTaskItemsRequest,
  CopyTaskItemsResponse,
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
  ReopenTaskItemRequest,
  RegisterUserRequest,
  RegisterUserResponse,
  SavedViewResponse,
  TaskTemplateDetailResponse,
  TaskTemplateSummaryResponse,
  TaskItemListQuery,
  TaskItemDetailResponse,
  TaskItemSummaryResponse,
  UpdateTaskItemRequest,
  UpdateTaskTimelineEntryRequest,
  UpdateArchiveResolutionRequest,
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
} from './types';

const configuredBaseUrl = import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, '');
const apiBaseUrl = configuredBaseUrl ?? '';
const sessionTokenStorageKey = 'dumptether.sessionToken';
const guestSessionTokenStorageKey = 'dumptether.guestSessionToken';
let currentWorkspaceId: string | null = null;
let currentSessionToken: string | null = readStoredSessionToken();
let sessionIsTemporary = readStoredTemporarySessionFlag();

export function setCurrentWorkspaceId(workspaceId: string | null) {
  currentWorkspaceId = workspaceId;
}

export function getStoredSessionToken() {
  return currentSessionToken;
}

export function getApiBaseUrl() {
  return apiBaseUrl;
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

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    credentials: 'include',
    headers: {
      ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
      ...(currentSessionToken ? { Authorization: `Bearer ${currentSessionToken}` } : {}),
      ...(currentWorkspaceId ? { 'X-DumpTether-Workspace-Id': currentWorkspaceId } : {}),
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

  return (await response.json()) as T;
}

function readStoredSessionToken() {
  return window.localStorage.getItem(sessionTokenStorageKey) ??
    window.sessionStorage.getItem(guestSessionTokenStorageKey);
}

function readStoredTemporarySessionFlag() {
  return !window.localStorage.getItem(sessionTokenStorageKey) &&
    Boolean(window.sessionStorage.getItem(guestSessionTokenStorageKey));
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

export function getAuthOptions(): Promise<AuthClientOptionsResponse> {
  return request<AuthClientOptionsResponse>('/api/auth/options');
}

export function beginOAuthLogin(provider: string) {
  const returnUrl = encodeURIComponent(window.location.href);
  window.location.assign(`${apiBaseUrl}/api/auth/oauth/${provider}?returnUrl=${returnUrl}`);
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

export function checkHealth(): Promise<{ status: string; service: string }> {
  return request<{ status: string; service: string }>('/health');
}

export function listTaskItems(
  query: TaskItemListQuery = {},
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
  );
}

export function getTaskItem(id: string): Promise<TaskItemDetailResponse> {
  return request<TaskItemDetailResponse>(`/api/tasks/${id}`);
}

export function createTaskItem(
  requestBody: CreateTaskItemRequest,
): Promise<TaskItemDetailResponse> {
  return request<TaskItemDetailResponse>('/api/tasks', {
    method: 'POST',
    body: JSON.stringify(requestBody),
  });
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
): Promise<TaskItemDetailResponse> {
  return request<TaskItemDetailResponse>(`/api/tasks/${id}`, {
    method: 'PATCH',
    body: JSON.stringify(requestBody),
  });
}

export function addTaskTimelineEntry(
  id: string,
  requestBody: AddTaskTimelineEntryRequest,
): Promise<TaskItemDetailResponse> {
  return request<TaskItemDetailResponse>(`/api/tasks/${id}/timeline`, {
    method: 'POST',
    body: JSON.stringify(requestBody),
  });
}

export function archiveTaskItem(
  id: string,
  requestBody: ArchiveTaskItemRequest,
): Promise<TaskItemDetailResponse> {
  return request<TaskItemDetailResponse>(`/api/tasks/${id}/archive`, {
    method: 'POST',
    body: JSON.stringify(requestBody),
  });
}

export function reopenTaskItem(
  id: string,
  requestBody: ReopenTaskItemRequest,
): Promise<TaskItemDetailResponse> {
  return request<TaskItemDetailResponse>(`/api/tasks/${id}/reopen`, {
    method: 'POST',
    body: JSON.stringify(requestBody),
  });
}

export function listArchiveResolutions(): Promise<ArchiveResolutionResponse[]> {
  return request<ArchiveResolutionResponse[]>('/api/archive-resolutions');
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

export function listProjects(): Promise<ProjectResponse[]> {
  return request<ProjectResponse[]>('/api/projects');
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

export function getWorkspace(): Promise<WorkspaceResponse> {
  return request<WorkspaceResponse>('/api/workspace');
}

export function listWorkspaces(): Promise<WorkspaceResponse[]> {
  return request<WorkspaceResponse[]>('/api/workspaces');
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

export function listWorkspaceMembers(): Promise<WorkspaceMemberResponse[]> {
  return request<WorkspaceMemberResponse[]>('/api/workspace/members');
}

export function listWorkspaceInvitations(): Promise<WorkspaceInvitationResponse[]> {
  return request<WorkspaceInvitationResponse[]>('/api/workspace/invitations');
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

export function revokeWorkspaceInvitation(id: string): Promise<void> {
  return request<void>(`/api/workspace/invitations/${id}`, {
    method: 'DELETE',
  });
}

export function listSavedViews(): Promise<SavedViewResponse[]> {
  return request<SavedViewResponse[]>('/api/views');
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
): Promise<TaskItemDetailResponse> {
  return request<TaskItemDetailResponse>(
    `/api/tasks/${taskItemId}/timeline/${entryId}`,
    {
      method: 'PATCH',
      body: JSON.stringify(requestBody),
    },
  );
}

export function deleteTaskTimelineEntry(
  taskItemId: string,
  entryId: string,
): Promise<TaskItemDetailResponse> {
  return request<TaskItemDetailResponse>(
    `/api/tasks/${taskItemId}/timeline/${entryId}`,
    {
      method: 'DELETE',
    },
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
): Promise<TaskShareLinkResponse> {
  return request<TaskShareLinkResponse>(`/api/tasks/${id}/share-links`, {
    method: 'POST',
    body: JSON.stringify(requestBody),
  });
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
): Promise<TaskItemDetailResponse> {
  return request<TaskItemDetailResponse>(`/api/tasks/${id}/shares/${shareId}`, {
    method: 'DELETE',
  });
}

export function listIncomingTaskShares(): Promise<TaskShareInboxResponse[]> {
  return request<TaskShareInboxResponse[]>('/api/account/task-shares');
}

export function leaveTaskShare(shareId: string): Promise<void> {
  return request<void>(`/api/account/task-shares/${shareId}`, {
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

export function listTaskTemplates(): Promise<TaskTemplateSummaryResponse[]> {
  return request<TaskTemplateSummaryResponse[]>('/api/templates');
}

export function getTaskTemplate(id: string): Promise<TaskTemplateDetailResponse> {
  return request<TaskTemplateDetailResponse>(`/api/templates/${id}`);
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
