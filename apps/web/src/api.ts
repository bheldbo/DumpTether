import type {
  AddTaskTimelineEntryRequest,
  ArchiveResolutionResponse,
  ArchiveTaskItemRequest,
  CreateSavedViewRequest,
  CreateTaskTemplateRequest,
  CreateTaskItemRequest,
  ProjectResponse,
  ReopenTaskItemRequest,
  SavedViewResponse,
  TaskTemplateDetailResponse,
  TaskTemplateSummaryResponse,
  TaskItemListQuery,
  TaskItemDetailResponse,
  TaskItemSummaryResponse,
  UpdateTaskItemRequest,
  UpdateSavedViewRequest,
  UpdateTaskTemplateRequest,
} from './types';

const configuredBaseUrl = import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, '');
const apiBaseUrl = configuredBaseUrl ?? '';

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
    headers: {
      ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
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

export function listProjects(): Promise<ProjectResponse[]> {
  return request<ProjectResponse[]>('/api/projects');
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
