namespace DumpTether.App.ArchiveResolutions;

public interface IArchiveResolutionService
{
    Task<IReadOnlyList<ArchiveResolutionResponse>> ListAsync(CancellationToken cancellationToken);
}
