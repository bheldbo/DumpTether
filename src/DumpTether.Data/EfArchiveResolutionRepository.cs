using DumpTether.App.ArchiveResolutions;
using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;

namespace DumpTether.Data;

internal sealed class EfArchiveResolutionRepository : IArchiveResolutionRepository
{
    private readonly DumpTetherDbContext _dbContext;

    public EfArchiveResolutionRepository(DumpTetherDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ArchiveResolution>> ListActiveAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ArchiveResolutions
            .AsNoTracking()
            .Where(archiveResolution =>
                archiveResolution.WorkspaceId == workspaceId &&
                archiveResolution.IsActive)
            .OrderBy(archiveResolution => archiveResolution.Name)
            .ToListAsync(cancellationToken);
    }
}
