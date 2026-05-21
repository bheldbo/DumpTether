export type TaskItemListScope = 'Active' | 'Archive' | 'All';

export interface TaskItemSummaryResponse {
  id: string;
  workspaceId: string;
  projectId: string | null;
  taskTemplateId: string | null;
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
  template: TaskTemplateDetailResponse | null;
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

export type FieldDefinitionType = 'Text' | 'LongText' | 'Date' | 'Checkbox' | 'Select';

export interface TaskTemplateSummaryResponse {
  id: string;
  name: string;
  createdAt: string;
  updatedAt: string;
  fieldCount: number;
}

export interface TaskTemplateDetailResponse {
  id: string;
  name: string;
  createdAt: string;
  updatedAt: string;
  fields: FieldDefinitionResponse[];
}

export interface FieldDefinitionResponse {
  id: string;
  key: string;
  name: string;
  type: FieldDefinitionType;
  required: boolean;
  sortOrder: number;
  options: string[];
}

export interface UpsertFieldDefinitionRequest {
  id?: string | null;
  name: string;
  type: FieldDefinitionType;
  required: boolean;
  sortOrder: number;
  options?: string[] | null;
}

export interface CreateTaskTemplateRequest {
  name: string;
  fields: UpsertFieldDefinitionRequest[];
}

export interface UpdateTaskTemplateRequest {
  name?: string;
  fields?: UpsertFieldDefinitionRequest[];
}

export type FieldValuePrimitive = string | boolean | null;

export type FieldValueMap = Record<string, FieldValuePrimitive>;

export interface CreateTaskItemRequest {
  title: string;
  taskTemplateId?: string | null;
  fieldValues?: FieldValueMap;
}

export interface UpdateTaskItemRequest {
  title?: string | null;
  status?: string | null;
  followUpAt?: string | null;
  fieldValues?: FieldValueMap;
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
