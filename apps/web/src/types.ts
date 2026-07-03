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
  shares: TaskItemShareResponse[];
  syncState: TaskSyncStateResponse | null;
  latestTimelineEntry: TaskTimelineEntryResponse | null;
}

export interface TaskItemDetailResponse extends TaskItemSummaryResponse {
  template: TaskTemplateDetailResponse | null;
  fieldValues: FieldValueResponse[];
  timelineEntries: TaskTimelineEntryResponse[];
}

export type TaskSyncStatus =
  | 'LocalOnly'
  | 'Synced'
  | 'Conflict'
  | 'Deleted'
  | 'SyncFailed'
  | string;

export interface TaskSyncStateResponse {
  status: TaskSyncStatus;
  remoteId: string | null;
  lastRemoteVersion: string | null;
  lastAttemptedAt: string | null;
  lastSyncedAt: string | null;
  lastError: string | null;
}

export type SyncRootStatus = 'LocalOnly' | 'Linked' | 'Conflict' | string;

export interface SyncRootResponse {
  id: string;
  localWorkspaceId: string;
  remoteWorkspaceId: string | null;
  cloudUserId: string | null;
  deviceId: string;
  status: SyncRootStatus;
  createdAt: string;
  updatedAt: string;
  lastSyncedAt: string | null;
}

export interface SyncWorkspaceWithCloudRequest {
  cloudApiBaseUrl: string;
  cloudSessionToken: string;
  remoteWorkspaceId?: string | null;
  pushLocalChanges?: boolean;
  pullRemoteChanges?: boolean;
}

export interface SyncWorkspaceWithCloudResponse {
  root: SyncRootResponse;
  taskStates: TaskSyncStateResponse[];
  pushed: number;
  pulled: number;
  updatedLocal: number;
  updatedRemote: number;
  conflicts: number;
  failed: number;
  messages: string[];
}

export interface TaskItemViewCountResponse {
  viewId: string;
  count: number;
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
  fieldValues: FieldValueResponse[];
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
  accessKind?: WorkspaceAccessKind;
  sharedTaskCount?: number;
  memberCount?: number;
  pendingInvitationCount?: number;
}

export type WorkspaceMembershipRole = 'Owner' | 'Member' | 'ReadOnly' | 'Guest' | 1 | 2 | 3;
export type WorkspaceAccessKind = 'Membership' | 'TaskShare';

export interface AuthUserResponse {
  id: string;
  email: string;
  displayName: string;
  createdAt: string;
  lastLoginAt: string | null;
  emailConfirmedAt: string | null;
}

export interface AuthWorkspaceResponse {
  id: string;
  name: string;
  color: string | null;
  role: WorkspaceMembershipRole;
  accessKind?: WorkspaceAccessKind;
  sharedTaskCount?: number;
}

export interface RegisterUserRequest {
  email: string;
  password: string;
  displayName?: string | null;
  inviteCode?: string | null;
}

export interface RegisterUserResponse {
  user: AuthUserResponse;
  workspace: AuthWorkspaceResponse;
  emailConfirmationRequired: boolean;
}

export interface LoginUserRequest {
  email: string;
  password: string;
  deviceName?: string | null;
}

export type UserSessionType =
  | 'Browser'
  | 'DesktopLocal'
  | 'DesktopCloud'
  | 'Development'
  | 'Guest'
  | 1
  | 2
  | 3
  | 4
  | 5;

export interface AuthSessionResponse {
  id: string;
  sessionType: UserSessionType;
  deviceName: string | null;
  createdAt: string;
  expiresAt: string;
  lastSeenAt: string;
}

export interface AuthSessionListItemResponse {
  id: string;
  sessionType: UserSessionType;
  deviceName: string | null;
  createdAt: string;
  expiresAt: string;
  lastSeenAt: string;
  revokedAt: string | null;
  isCurrent: boolean;
}

export interface LoginUserResponse {
  user: AuthUserResponse;
  workspaces: AuthWorkspaceResponse[];
  sessionToken: string;
  expiresAt: string;
  session: AuthSessionResponse;
}

export interface CurrentUserResponse {
  user: AuthUserResponse;
  workspaces: AuthWorkspaceResponse[];
  session: AuthSessionResponse;
}

export interface AuthClientOptionsResponse {
  requiresAuthentication: boolean;
  guestSessionsEnabled: boolean;
  developmentLoginEnabled: boolean;
  localDesktopLoginEnabled: boolean;
  emailConfirmationEnabled: boolean;
  signupMode: 'Open' | 'Whitelist' | 'InviteOnly' | 'Closed' | 1 | 2 | 3 | 4;
  oAuthProviders: string[];
}

export interface UpdateWorkspaceRequest {
  name?: string | null;
  color?: string | null;
}

export interface CreateWorkspaceRequest {
  name: string;
  color?: string | null;
}

export interface WorkspaceMemberResponse {
  userId: string;
  email: string;
  displayName: string;
  role: WorkspaceMembershipRole;
  createdAt: string;
}

export interface WorkspaceInvitationResponse {
  id: string;
  workspaceId: string;
  email: string;
  role: WorkspaceMembershipRole;
  createdAt: string;
  expiresAt: string;
  acceptedAt: string | null;
  revokedAt: string | null;
  token: string | null;
}

export interface CreateWorkspaceInvitationRequest {
  email: string;
  role?: WorkspaceMembershipRole;
}

export interface UpdateWorkspaceMemberRequest {
  role: WorkspaceMembershipRole;
}

export interface AcceptWorkspaceInvitationRequest {
  token?: string | null;
  invitationId?: string | null;
}

export interface WorkspaceInvitationInboxResponse {
  id: string;
  workspaceId: string;
  workspaceName: string;
  workspaceColor: string | null;
  invitedByEmail: string;
  invitedByDisplayName: string;
  role: WorkspaceMembershipRole;
  createdAt: string;
  expiresAt: string;
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

export type TaskItemShareRole = 'Viewer' | 'Editor' | 'ReadOnly' | 'Member' | 1 | 2;

export interface TaskItemShareResponse {
  id: string;
  email: string;
  sharedWithUserId: string | null;
  sharedByUserId: string;
  role: TaskItemShareRole;
  createdAt: string;
  expiresAt: string | null;
  acceptedAt: string | null;
  revokedAt: string | null;
}

export interface TaskShareInboxResponse {
  shareId: string;
  taskItemId: string;
  workspaceId: string;
  workspaceName: string;
  workspaceColor: string | null;
  taskTitle: string;
  sharedByEmail: string;
  sharedByDisplayName: string;
  role: TaskItemShareRole;
  createdAt: string;
  expiresAt: string | null;
  acceptedAt: string | null;
}

export interface TaskShareLinkResponse {
  shares: TaskItemShareResponse[];
  token: string;
  expiresAt: string;
}

export interface CreateTaskShareLinkRequest {
  email: string;
  taskItemIds?: string[] | null;
  role?: TaskItemShareRole;
}

export interface UpdateTaskShareRequest {
  role: TaskItemShareRole;
}

export interface AcceptShareLinkRequest {
  token: string;
}

export interface ShareLinkAcceptResponse {
  kind: 'Workspace' | 'Task' | string;
  workspaceId: string;
  taskItemIds: string[];
}

export interface CopyTaskItemsRequest {
  taskItemIds: string[];
  destinationWorkspaceId: string;
  includeTimeline?: boolean;
}

export interface CopyTaskItemsResponse {
  tasks: TaskItemDetailResponse[];
}

export interface TaskItemBatchResponse {
  count: number;
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
export type FieldDefinitionScope = 'Header' | 'Entry';

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
  layout: TaskTemplateLayoutResponse;
  fields: FieldDefinitionResponse[];
}

export interface TaskTemplateLayoutResponse {
  header: TaskTemplateLayoutRow[];
  entry: TaskTemplateLayoutRow[];
}

export interface TaskTemplateLayoutRow {
  row: number;
  columnWeights: number[];
  height: number;
}

export interface FieldDefinitionResponse {
  id: string;
  key: string;
  name: string;
  type: FieldDefinitionType;
  scope: FieldDefinitionScope;
  required: boolean;
  sortOrder: number;
  options: string[];
  layoutRow: number;
  layoutColumn: number;
  layoutRowSpan: number;
  layoutColumnSpan: number;
  layoutWeight: number;
}

export interface UpsertFieldDefinitionRequest {
  id?: string | null;
  name: string;
  type: FieldDefinitionType;
  scope?: FieldDefinitionScope | null;
  required: boolean;
  sortOrder: number;
  options?: string[] | null;
  layoutRow?: number | null;
  layoutColumn?: number | null;
  layoutRowSpan?: number | null;
  layoutColumnSpan?: number | null;
  layoutWeight?: number | null;
}

export interface CreateTaskTemplateRequest {
  name: string;
  fields: UpsertFieldDefinitionRequest[];
  layout?: TaskTemplateLayoutResponse | null;
}

export interface UpdateTaskTemplateRequest {
  name?: string;
  fields?: UpsertFieldDefinitionRequest[];
  layout?: TaskTemplateLayoutResponse | null;
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
  note?: string | null;
  fieldValues?: FieldValueMap;
}

export interface UpdateTaskTimelineEntryRequest {
  note?: string | null;
  fieldValues?: FieldValueMap;
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

export interface ReopenTaskItemsRequest {
  taskItemIds: string[];
  note?: string | null;
}

export interface DeleteTaskItemsRequest {
  taskItemIds: string[];
}

export interface CreateTaskShareRequest {
  email: string;
  role?: TaskItemShareRole;
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
  sharedWith?: string | null;
  sharedWithMe?: boolean | null;
  sort?: SavedViewSortField | null;
  direction?: SavedViewSortDirection | null;
}
