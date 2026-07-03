using DumpTether.App.Tasks;

namespace DumpTether.App.Sync;

public interface ISyncService
{
    Task<IReadOnlyList<SyncRootResponse>> ListWorkspaceRootsAsync(
        CancellationToken cancellationToken);

    Task<SyncRootResponse> EnsureWorkspaceRootAsync(
        EnsureWorkspaceSyncRootRequest request,
        CancellationToken cancellationToken);

    Task<SyncRootResponse> LinkWorkspaceRootAsync(
        LinkWorkspaceSyncRootRequest request,
        CancellationToken cancellationToken);

    Task EnsureLocalTaskMappingAsync(
        Guid workspaceId,
        Guid taskItemId,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, TaskSyncStateResponse>> ListTaskSyncStatesAsync(
        Guid workspaceId,
        IReadOnlyCollection<Guid> taskItemIds,
        CancellationToken cancellationToken);

    Task<TaskSyncStateResponse> MarkTaskItemSyncedAsync(
        Guid workspaceId,
        Guid taskItemId,
        MarkTaskItemSyncedRequest request,
        CancellationToken cancellationToken);

    Task<TaskSyncStateResponse> MarkTaskItemSyncFailedAsync(
        Guid workspaceId,
        Guid taskItemId,
        MarkTaskItemSyncFailedRequest request,
        CancellationToken cancellationToken);
}
