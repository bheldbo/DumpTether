using DumpTether.App.Tasks;
using DumpTether.Domain;

namespace DumpTether.App.ArchiveResolutions;

internal sealed class ArchiveResolutionService : IArchiveResolutionService
{
    private readonly IArchiveResolutionRepository _archiveResolutionRepository;
    private readonly IDevelopmentWorkspaceProvider _developmentWorkspaceProvider;

    public ArchiveResolutionService(
        IArchiveResolutionRepository archiveResolutionRepository,
        IDevelopmentWorkspaceProvider developmentWorkspaceProvider)
    {
        _archiveResolutionRepository = archiveResolutionRepository;
        _developmentWorkspaceProvider = developmentWorkspaceProvider;
    }

    public async Task<IReadOnlyList<ArchiveResolutionResponse>> ListAsync(
        CancellationToken cancellationToken)
    {
        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var archiveResolutions = await _archiveResolutionRepository.ListActiveAsync(
            context.WorkspaceId,
            cancellationToken);

        return archiveResolutions
            .OrderBy(archiveResolution => archiveResolution.Name)
            .Select(Map)
            .ToList();
    }

    private static ArchiveResolutionResponse Map(ArchiveResolution archiveResolution)
    {
        return new ArchiveResolutionResponse(
            archiveResolution.Id,
            archiveResolution.Name,
            archiveResolution.Description,
            archiveResolution.RequiresExplanation);
    }
}
