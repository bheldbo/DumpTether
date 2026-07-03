using DumpTether.App.Tasks;
using DumpTether.Domain;

namespace DumpTether.App.Sync;

public sealed record CloudSyncConnection(
    string BaseUrl,
    string SessionToken);

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
    string CloudApiBaseUrl,
    string CloudSessionToken,
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
    DateTimeOffset? ArchivedAt);

public sealed record CloudSyncCreateWorkspaceRequest(
    string Name,
    string? Color);

public sealed record CloudSyncCreateTaskRequest(
    string Title,
    string? Status,
    string? Category,
    string? Color,
    DateTimeOffset? FollowUpAt);

public sealed record CloudSyncUpdateTaskRequest(
    string? Title,
    string? Status,
    string? Category,
    string? Color,
    DateTimeOffset? FollowUpAt);
