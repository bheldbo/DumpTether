using System.ComponentModel.DataAnnotations;
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

    public async Task<ArchiveResolutionResponse> CreateAsync(
        CreateArchiveResolutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        EnsureCanWriteWorkspace(context);
        var archiveResolution = ArchiveResolution.Create(
            context.WorkspaceId,
            request.Name,
            DateTimeOffset.UtcNow,
            request.Description,
            request.RequiresExplanation);

        await _archiveResolutionRepository.AddAsync(archiveResolution, cancellationToken);
        await _archiveResolutionRepository.SaveChangesAsync(cancellationToken);

        return Map(archiveResolution);
    }

    public async Task<ArchiveResolutionResponse?> UpdateAsync(
        Guid id,
        UpdateArchiveResolutionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        EnsureCanWriteWorkspace(context);
        var archiveResolution = await _archiveResolutionRepository.GetByIdAsync(
            id,
            context.WorkspaceId,
            cancellationToken);

        if (archiveResolution is null)
        {
            return null;
        }

        archiveResolution.Update(
            request.Name ?? archiveResolution.Name,
            request.Description ?? archiveResolution.Description,
            request.RequiresExplanation ?? archiveResolution.RequiresExplanation);
        await _archiveResolutionRepository.SaveChangesAsync(cancellationToken);

        return Map(archiveResolution);
    }

    public async Task<bool> DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        EnsureCanWriteWorkspace(context);
        var archiveResolution = await _archiveResolutionRepository.GetByIdAsync(
            id,
            context.WorkspaceId,
            cancellationToken);

        if (archiveResolution is null)
        {
            return false;
        }

        archiveResolution.Deactivate();
        await _archiveResolutionRepository.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static void EnsureCanWriteWorkspace(DevelopmentWorkspaceContext context)
    {
        if (!context.CanWriteWorkspace)
        {
            throw new ValidationException("Read-only board access cannot change archive reasons.");
        }
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
