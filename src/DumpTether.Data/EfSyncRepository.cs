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

    public async Task AddRootAsync(
        SyncRoot syncRoot,
        CancellationToken cancellationToken)
    {
        await _dbContext.SyncRoots.AddAsync(syncRoot, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
