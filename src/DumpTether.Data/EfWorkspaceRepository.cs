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

    public async Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.Workspaces
            .SingleOrDefaultAsync(workspace => workspace.Id == id, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
