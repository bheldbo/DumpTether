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
}
