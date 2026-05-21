using DumpTether.App.Views;
using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;

namespace DumpTether.Data;

internal sealed class EfSavedViewRepository : ISavedViewRepository
{
    private readonly DumpTetherDbContext _dbContext;

    public EfSavedViewRepository(DumpTetherDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(SavedView savedView, CancellationToken cancellationToken)
    {
        await _dbContext.SavedViews.AddAsync(savedView, cancellationToken);
    }

    public async Task<IReadOnlyList<SavedView>> ListAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.SavedViews
            .AsNoTracking()
            .Where(savedView =>
                savedView.WorkspaceId == workspaceId &&
                savedView.DeletedAt == null)
            .OrderBy(savedView => savedView.SortOrder)
            .ThenBy(savedView => savedView.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<SavedView?> GetByIdAsync(
        Guid id,
        Guid workspaceId,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.SavedViews
            .Where(savedView =>
                savedView.Id == id &&
                savedView.WorkspaceId == workspaceId &&
                savedView.DeletedAt == null);

        if (!trackChanges)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> AnyActiveWithNameAsync(
        Guid workspaceId,
        string name,
        Guid? excludedSavedViewId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.SavedViews
            .AnyAsync(
                savedView =>
                    savedView.WorkspaceId == workspaceId &&
                    savedView.DeletedAt == null &&
                    savedView.Name == name &&
                    (!excludedSavedViewId.HasValue || savedView.Id != excludedSavedViewId.Value),
                cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
