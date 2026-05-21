export type TaskItemListScope = 'Active' | 'Archive' | 'All';

export interface TaskItemSummaryResponse {
  id: string;
  workspaceId: string;
  projectId: string | null;
  title: string;
  status: string | null;
  createdAt: string;
  lastViewedAt: string | null;
  lastTouchedAt: string;
  followUpAt: string | null;
  archivedAt: string | null;
  archiveResolutionId: string | null;
}

export interface TaskItemDetailResponse extends TaskItemSummaryResponse {
  fieldValues: FieldValueResponse[];
  timelineEntries: TaskTimelineEntryResponse[];
}

export interface FieldValueResponse {
  id: string;
  fieldDefinitionId: string;
  valueJson: string;
  updatedAt: string;
}

export interface TaskTimelineEntryResponse {
  id: string;
  kind: string;
  summary: string;
  details: string | null;
  occurredAt: string;
}

export interface ArchiveResolutionResponse {
  id: string;
  name: string;
  description: string | null;
  requiresExplanation: boolean;
}

export interface CreateTaskItemRequest {
  title: string;
}

export interface AddTaskTimelineEntryRequest {
  note: string;
}

export interface ArchiveTaskItemRequest {
  archiveResolutionId: string;
  note?: string | null;
}

export interface ReopenTaskItemRequest {
  note?: string | null;
}
