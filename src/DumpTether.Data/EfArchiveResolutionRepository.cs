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

    public async Task<ArchiveResolution?> GetByIdAsync(
        Guid id,
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.ArchiveResolutions
            .SingleOrDefaultAsync(
                archiveResolution =>
                    archiveResolution.Id == id &&
                    archiveResolution.WorkspaceId == workspaceId &&
                    archiveResolution.IsActive,
                cancellationToken);
    }

    public async Task AddAsync(
        ArchiveResolution archiveResolution,
        CancellationToken cancellationToken)
    {
        await _dbContext.ArchiveResolutions.AddAsync(archiveResolution, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
