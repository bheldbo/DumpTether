using DumpTether.Domain;

namespace DumpTether.App.ArchiveResolutions;

public interface IArchiveResolutionRepository
{
    Task<IReadOnlyList<ArchiveResolution>> ListActiveAsync(
        Guid workspaceId,
        CancellationToken cancellationToken);
}
