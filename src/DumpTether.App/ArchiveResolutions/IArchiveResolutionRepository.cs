using DumpTether.Domain;

namespace DumpTether.App.ArchiveResolutions;

public interface IArchiveResolutionRepository
{
    Task<IReadOnlyList<ArchiveResolution>> ListActiveAsync(
        Guid workspaceId,
        CancellationToken cancellationToken);

    Task<ArchiveResolution?> GetByIdAsync(
        Guid id,
        Guid workspaceId,
        CancellationToken cancellationToken);

    Task AddAsync(
        ArchiveResolution archiveResolution,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
