using DumpTether.App.Projects;
using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;

namespace DumpTether.Data;

internal sealed class EfProjectRepository : IProjectRepository
{
    private readonly DumpTetherDbContext _dbContext;

    public EfProjectRepository(DumpTetherDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Project>> ListAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Projects
            .AsNoTracking()
            .Where(project => project.WorkspaceId == workspaceId)
            .OrderBy(project => project.Name)
            .ToListAsync(cancellationToken);
    }
}
