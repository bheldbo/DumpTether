using DumpTether.App.Tasks;
using DumpTether.Domain;

namespace DumpTether.App.Sync;

public sealed record CloudSyncConnection(
    string BaseUrl,
    string SessionToken);

public sealed record CloudSyncLoginRequest(
    string Email,
    string Password,
    string? DeviceName = null);

public sealed record CloudSyncLoginResponse(
    CloudSyncUserResponse User,
    string SessionToken,
    DateTimeOffset ExpiresAt);

public sealed record ConnectCloudAccountRequest(
    string CloudApiBaseUrl,
    string Email,
    string Password,
    string? DeviceName = null);

public sealed record CloudSyncAccountResponse(
    Guid Id,
    string CloudApiBaseUrl,
    Guid CloudUserId,
    string CloudEmail,
    string CloudDisplayName,
    DateTimeOffset SessionExpiresAt,
    DateTimeOffset ConnectedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastVerifiedAt,
    bool IsConnected);

public sealed record DisconnectCloudAccountResponse(
    bool Disconnected);

public sealed record EnsureWorkspaceSyncRootRequest(
    Guid LocalWorkspaceId,
    string DeviceId);

public sealed record LinkWorkspaceSyncRootRequest(
    Guid LocalWorkspaceId,
    Guid RemoteWorkspaceId,
    Guid CloudUserId,
    string DeviceId);

public sealed record SyncRootResponse(
    Guid Id,
    Guid LocalWorkspaceId,
    Guid? RemoteWorkspaceId,
    Guid? CloudUserId,
    string DeviceId,
    SyncRootStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastSyncedAt);

public sealed record MarkTaskItemSyncedRequest(
    Guid RemoteTaskItemId,
    string? RemoteVersion = null);

public sealed record MarkTaskItemSyncFailedRequest(
    string Error);

public sealed record SyncWorkspaceWithCloudRequest(
    string? CloudApiBaseUrl = null,
    string? CloudSessionToken = null,
    Guid? RemoteWorkspaceId = null,
    bool PushLocalChanges = true,
    bool PullRemoteChanges = true);

public sealed record SyncWorkspaceWithCloudResponse(
    SyncRootResponse Root,
    IReadOnlyList<TaskSyncStateResponse> TaskStates,
    int Pushed,
    int Pulled,
    int UpdatedLocal,
    int UpdatedRemote,
    int Conflicts,
    int Failed,
    IReadOnlyList<string> Messages);

public sealed record CloudSyncUserResponse(
    Guid Id,
    string Email,
    string DisplayName);

public sealed record CloudSyncWorkspaceResponse(
    Guid Id,
    string Name,
    string? Color);

public sealed record CloudSyncTaskResponse(
    Guid Id,
    Guid WorkspaceId,
    Guid? TaskTemplateId,
    string Title,
    string? Status,
    string? Category,
    string? Color,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastTouchedAt,
    DateTimeOffset? FollowUpAt,
    DateTimeOffset? ArchivedAt,
    IReadOnlyList<CloudSyncFieldValueResponse>? FieldValues = null);

public sealed record CloudSyncCreateWorkspaceRequest(
    string Name,
    string? Color);

public sealed record CloudSyncCreateTaskRequest(
    string Title,
    Guid? TaskTemplateId,
    string? Status,
    string? Category,
    string? Color,
    DateTimeOffset? FollowUpAt,
    IReadOnlyDictionary<Guid, string>? FieldValues = null);

public sealed record CloudSyncUpdateTaskRequest(
    string? Title,
    Guid? TaskTemplateId,
    string? Status,
    string? Category,
    string? Color,
    DateTimeOffset? FollowUpAt,
    IReadOnlyDictionary<Guid, string>? FieldValues = null);

public sealed record CloudSyncFieldValueResponse(
    Guid FieldDefinitionId,
    string ValueJson,
    DateTimeOffset UpdatedAt);

public sealed record CloudSyncTaskTemplateResponse(
    Guid Id,
    string Name,
    DateTimeOffset UpdatedAt,
    CloudSyncTaskTemplateLayoutResponse Layout,
    IReadOnlyList<CloudSyncFieldDefinitionResponse> Fields);

public sealed record CloudSyncTaskTemplateLayoutResponse(
    IReadOnlyList<CloudSyncTaskTemplateLayoutRowResponse> Header,
    IReadOnlyList<CloudSyncTaskTemplateLayoutRowResponse> Entry);

public sealed record CloudSyncTaskTemplateLayoutRowResponse(
    int Row,
    IReadOnlyList<double> ColumnWeights,
    double Height);

public sealed record CloudSyncFieldDefinitionResponse(
    Guid Id,
    string Key,
    string Name,
    string Type,
    string Scope,
    bool Required,
    int SortOrder,
    IReadOnlyList<string> Options,
    int LayoutRow,
    int LayoutColumn,
    int LayoutRowSpan,
    int LayoutColumnSpan,
    double LayoutWeight);

public sealed record CloudSyncUpsertFieldDefinitionRequest(
    Guid? Id,
    string Name,
    string Type,
    string Scope,
    bool Required,
    int SortOrder,
    IReadOnlyList<string> Options,
    int LayoutRow,
    int LayoutColumn,
    int LayoutRowSpan,
    int LayoutColumnSpan,
    double LayoutWeight);

public sealed record CloudSyncTaskTemplateLayoutRequest(
    IReadOnlyList<CloudSyncTaskTemplateLayoutRowRequest> Header,
    IReadOnlyList<CloudSyncTaskTemplateLayoutRowRequest> Entry);

public sealed record CloudSyncTaskTemplateLayoutRowRequest(
    int Row,
    IReadOnlyList<double> ColumnWeights,
    double Height);

public sealed record CloudSyncCreateTaskTemplateRequest(
    string Name,
    IReadOnlyList<CloudSyncUpsertFieldDefinitionRequest> Fields,
    CloudSyncTaskTemplateLayoutRequest Layout);

public sealed record CloudSyncUpdateTaskTemplateRequest(
    string Name,
    IReadOnlyList<CloudSyncUpsertFieldDefinitionRequest> Fields,
    CloudSyncTaskTemplateLayoutRequest Layout);
