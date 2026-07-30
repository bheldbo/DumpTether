using DumpTether.Domain;

namespace DumpTether.App.Sync;

public interface ISyncRepository
{
    Task<IReadOnlyList<SyncRoot>> ListRootsForLocalWorkspacesAsync(
        IReadOnlyCollection<Guid> localWorkspaceIds,
        CancellationToken cancellationToken);

    Task<SyncRoot?> GetRootByLocalWorkspaceAsync(
        Guid localWorkspaceId,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task<SyncRoot?> GetRootByRemoteWorkspaceAsync(
        Guid remoteWorkspaceId,
        Guid cloudUserId,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task<SyncMapping?> GetMappingAsync(
        Guid syncRootId,
        SyncEntityType entityType,
        Guid localId,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SyncMapping>> ListMappingsAsync(
        Guid syncRootId,
        SyncEntityType entityType,
        IReadOnlyCollection<Guid> localIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SyncMapping>> ListMappingsForRootAsync(
        Guid syncRootId,
        SyncEntityType entityType,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task<CloudSyncAccount?> GetCloudAccountForUserAsync(
        Guid userId,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CloudSyncAccount>> ListConnectedCloudAccountsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task AddRootAsync(SyncRoot syncRoot, CancellationToken cancellationToken);

    Task AddMappingAsync(SyncMapping mapping, CancellationToken cancellationToken);

    Task AddCloudAccountAsync(CloudSyncAccount account, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
