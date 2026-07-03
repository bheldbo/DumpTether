using DumpTether.Domain;

namespace DumpTether.App.Sync;

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
