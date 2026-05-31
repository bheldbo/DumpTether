using DumpTether.App.Workspaces;
using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;

namespace DumpTether.Data;

internal sealed class EfWorkspaceRepository : IWorkspaceRepository
{
    private readonly DumpTetherDbContext _dbContext;

    public EfWorkspaceRepository(DumpTetherDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Workspace>> ListAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Workspaces
            .AsNoTracking()
            .OrderBy(workspace => workspace.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Workspace>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.WorkspaceMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == userId)
            .Join(
                _dbContext.Workspaces.AsNoTracking(),
                membership => membership.WorkspaceId,
                workspace => workspace.Id,
                (_, workspace) => workspace)
            .OrderBy(workspace => workspace.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.Workspaces
            .SingleOrDefaultAsync(workspace => workspace.Id == id, cancellationToken);
    }

    public async Task AddAsync(Workspace workspace, CancellationToken cancellationToken)
    {
        await _dbContext.Workspaces.AddAsync(workspace, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
