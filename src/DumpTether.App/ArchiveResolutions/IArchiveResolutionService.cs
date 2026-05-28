namespace DumpTether.App.ArchiveResolutions;

public interface IArchiveResolutionService
{
    Task<IReadOnlyList<ArchiveResolutionResponse>> ListAsync(CancellationToken cancellationToken);

    Task<ArchiveResolutionResponse> CreateAsync(
        CreateArchiveResolutionRequest request,
        CancellationToken cancellationToken);

    Task<ArchiveResolutionResponse?> UpdateAsync(
        Guid id,
        UpdateArchiveResolutionRequest request,
        CancellationToken cancellationToken);

    Task<bool> DeactivateAsync(Guid id, CancellationToken cancellationToken);
}
