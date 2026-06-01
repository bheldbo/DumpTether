export type TaskItemListScope = 'Active' | 'Archive' | 'All';

export interface TaskItemSummaryResponse {
  id: string;
  workspaceId: string;
  projectId: string | null;
  taskTemplateId: string | null;
  title: string;
  status: string | null;
  category: string | null;
  color: string | null;
  createdAt: string;
  lastViewedAt: string | null;
  lastTouchedAt: string;
  followUpAt: string | null;
  archivedAt: string | null;
  archiveResolutionId: string | null;
  noteCount: number;
  latestTimelineEntry: TaskTimelineEntryResponse | null;
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
  updatedAt: string;
}

export interface ArchiveResolutionResponse {
  id: string;
  name: string;
  description: string | null;
  requiresExplanation: boolean;
}

export interface CreateArchiveResolutionRequest {
  name: string;
  description?: string | null;
  requiresExplanation?: boolean;
}

export interface UpdateArchiveResolutionRequest {
  name?: string | null;
  description?: string | null;
  requiresExplanation?: boolean | null;
}

export interface ProjectResponse {
  id: string;
  workspaceId: string;
  name: string;
  color: string | null;
  createdAt: string;
  isActive: boolean;
}

export interface ProjectArchiveResponse {
  id: string;
  archivedTaskCount: number;
}

export interface WorkspaceResponse {
  id: string;
  name: string;
  color: string | null;
  createdAt: string;
}

export type WorkspaceMembershipRole = 'Owner' | 'Member' | 1 | 2;

export interface AuthUserResponse {
  id: string;
  email: string;
  displayName: string;
  createdAt: string;
  lastLoginAt: string | null;
}

export interface AuthWorkspaceResponse {
  id: string;
  name: string;
  color: string | null;
  role: WorkspaceMembershipRole;
}

export interface RegisterUserRequest {
  email: string;
  password: string;
  displayName?: string | null;
}

export interface RegisterUserResponse {
  user: AuthUserResponse;
  workspace: AuthWorkspaceResponse;
}

export interface LoginUserRequest {
  email: string;
  password: string;
  deviceName?: string | null;
}

export interface LoginUserResponse {
  user: AuthUserResponse;
  workspaces: AuthWorkspaceResponse[];
  sessionToken: string;
  expiresAt: string;
}

export interface CurrentUserResponse {
  user: AuthUserResponse;
  workspaces: AuthWorkspaceResponse[];
}

export interface AuthClientOptionsResponse {
  requiresAuthentication: boolean;
  guestSessionsEnabled: boolean;
  developmentLoginEnabled: boolean;
}

export interface UpdateWorkspaceRequest {
  name?: string | null;
  color?: string | null;
}

export interface CreateWorkspaceRequest {
  name: string;
  color?: string | null;
}

export interface UpdateProjectRequest {
  name?: string | null;
  color?: string | null;
}

export interface CreateProjectRequest {
  name: string;
  color?: string | null;
}

export type SavedViewScope = 'Workspace' | 'Project';
export type SavedViewArchiveFilter = 'Active' | 'Archived' | 'All';
export type SavedViewFollowUpFilter = 'Any' | 'Overdue' | 'Today' | 'ThisWeek';
export type SavedViewSortField =
  | 'lastTouchedAt'
  | 'createdAt'
  | 'followUpAt'
  | 'title'
  | 'status';
export type SavedViewSortDirection = 'asc' | 'desc';

export interface SavedViewFilter {
  projectId?: string | null;
  status?: string | null;
  category?: string | null;
  color?: string | null;
  archive?: SavedViewArchiveFilter | null;
  followUp?: SavedViewFollowUpFilter | null;
  notViewedSinceDays?: number | null;
  notTouchedSinceDays?: number | null;
  text?: string | null;
}

export interface SavedViewSort {
  field?: SavedViewSortField | null;
  direction?: SavedViewSortDirection | null;
}

export interface SavedViewResponse {
  id: string;
  workspaceId: string;
  projectId: string | null;
  name: string;
  scope: SavedViewScope;
  filter: SavedViewFilter;
  sort: SavedViewSort;
  sortOrder: number;
  createdAt: string;
  updatedAt: string;
}

export interface CreateSavedViewRequest {
  name: string;
  scope?: SavedViewScope | null;
  filter?: SavedViewFilter | null;
  sort?: SavedViewSort | null;
  sortOrder?: number;
}

export interface UpdateSavedViewRequest {
  name?: string;
  scope?: SavedViewScope | null;
  filter?: SavedViewFilter | null;
  sort?: SavedViewSort | null;
  sortOrder?: number;
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
  projectId?: string | null;
  category?: string | null;
}

export interface UpdateTaskItemRequest {
  title?: string | null;
  status?: string | null;
  category?: string | null;
  color?: string | null;
  followUpAt?: string | null;
  fieldValues?: FieldValueMap;
  projectId?: string | null;
}

export interface AddTaskTimelineEntryRequest {
  note: string;
}

export interface UpdateTaskTimelineEntryRequest {
  note: string;
}

export interface ArchiveTaskItemRequest {
  archiveResolutionId: string;
  note?: string | null;
}

export interface ArchiveProjectTasksRequest {
  archiveResolutionId: string;
  note?: string | null;
}

export interface ReopenTaskItemRequest {
  note?: string | null;
}

export interface TaskItemListQuery {
  viewId?: string | null;
  scope?: TaskItemListScope;
  projectId?: string | null;
  status?: string | null;
  category?: string | null;
  color?: string | null;
  archive?: SavedViewArchiveFilter | null;
  followUp?: SavedViewFollowUpFilter | null;
  notViewedSinceDays?: number | null;
  notTouchedSinceDays?: number | null;
  text?: string | null;
  sort?: SavedViewSortField | null;
  direction?: SavedViewSortDirection | null;
}
