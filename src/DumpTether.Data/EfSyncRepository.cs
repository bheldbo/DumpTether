using DumpTether.App.Sync;
using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;

namespace DumpTether.Data;

internal sealed class EfSyncRepository : ISyncRepository
{
    private readonly DumpTetherDbContext _dbContext;

    public EfSyncRepository(DumpTetherDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<SyncRoot>> ListRootsForLocalWorkspacesAsync(
        IReadOnlyCollection<Guid> localWorkspaceIds,
        CancellationToken cancellationToken)
    {
        if (localWorkspaceIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.SyncRoots
            .AsNoTracking()
            .Where(syncRoot => localWorkspaceIds.Contains(syncRoot.LocalWorkspaceId))
            .ToListAsync(cancellationToken);
    }

    public async Task<SyncRoot?> GetRootByLocalWorkspaceAsync(
        Guid localWorkspaceId,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.SyncRoots
            .Where(syncRoot => syncRoot.LocalWorkspaceId == localWorkspaceId);

        if (!trackChanges)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<SyncRoot?> GetRootByRemoteWorkspaceAsync(
        Guid remoteWorkspaceId,
        Guid cloudUserId,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.SyncRoots
            .Where(syncRoot =>
                syncRoot.RemoteWorkspaceId == remoteWorkspaceId &&
                syncRoot.CloudUserId == cloudUserId);

        if (!trackChanges)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<SyncMapping?> GetMappingAsync(
        Guid syncRootId,
        SyncEntityType entityType,
        Guid localId,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.SyncMappings
            .Where(mapping =>
                mapping.SyncRootId == syncRootId &&
                mapping.EntityType == entityType &&
                mapping.LocalId == localId);

        if (!trackChanges)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SyncMapping>> ListMappingsAsync(
        Guid syncRootId,
        SyncEntityType entityType,
        IReadOnlyCollection<Guid> localIds,
        CancellationToken cancellationToken)
    {
        if (localIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.SyncMappings
            .AsNoTracking()
            .Where(mapping =>
                mapping.SyncRootId == syncRootId &&
                mapping.EntityType == entityType &&
                localIds.Contains(mapping.LocalId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SyncMapping>> ListMappingsForRootAsync(
        Guid syncRootId,
        SyncEntityType entityType,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.SyncMappings
            .Where(mapping =>
                mapping.SyncRootId == syncRootId &&
                mapping.EntityType == entityType);

        if (!trackChanges)
        {
            query = query.AsNoTracking();
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<CloudSyncAccount?> GetCloudAccountForUserAsync(
        Guid userId,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.CloudSyncAccounts
            .Where(account => account.UserId == userId);

        if (!trackChanges)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CloudSyncAccount>> ListConnectedCloudAccountsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var candidates = await _dbContext.CloudSyncAccounts
            .AsNoTracking()
            .Where(account => account.DisconnectedAt == null)
            .ToListAsync(cancellationToken);

        // SQLite cannot order or compare DateTimeOffset values server-side.
        return candidates
            .Where(account =>
                account.HasUsableSession(now) &&
                !string.IsNullOrWhiteSpace(account.ProtectedSessionToken))
            .ToList();
    }

    public async Task AddRootAsync(
        SyncRoot syncRoot,
        CancellationToken cancellationToken)
    {
        await _dbContext.SyncRoots.AddAsync(syncRoot, cancellationToken);
    }

    public async Task AddMappingAsync(
        SyncMapping mapping,
        CancellationToken cancellationToken)
    {
        await _dbContext.SyncMappings.AddAsync(mapping, cancellationToken);
    }

    public async Task AddCloudAccountAsync(
        CloudSyncAccount account,
        CancellationToken cancellationToken)
    {
        await _dbContext.CloudSyncAccounts.AddAsync(account, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
