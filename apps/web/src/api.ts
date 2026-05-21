import type {
  AddTaskTimelineEntryRequest,
  ArchiveResolutionResponse,
  ArchiveTaskItemRequest,
  CreateTaskItemRequest,
  ReopenTaskItemRequest,
  TaskItemDetailResponse,
  TaskItemListScope,
  TaskItemSummaryResponse,
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
  scope: TaskItemListScope,
): Promise<TaskItemSummaryResponse[]> {
  return request<TaskItemSummaryResponse[]>(
    `/api/tasks?scope=${encodeURIComponent(scope)}`,
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
