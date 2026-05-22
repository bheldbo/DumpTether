using DumpTether.App.Tasks;
using DumpTether.Domain;

namespace DumpTether.App.Workspaces;

internal sealed class WorkspaceService : IWorkspaceService
{
    private readonly IDevelopmentWorkspaceProvider _developmentWorkspaceProvider;
    private readonly IWorkspaceRepository _workspaceRepository;

    public WorkspaceService(
        IDevelopmentWorkspaceProvider developmentWorkspaceProvider,
        IWorkspaceRepository workspaceRepository)
    {
        _developmentWorkspaceProvider = developmentWorkspaceProvider;
        _workspaceRepository = workspaceRepository;
    }

    public async Task<WorkspaceResponse> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var workspace = await GetCurrentWorkspaceAsync(cancellationToken);
        return MapWorkspace(workspace);
    }

    public async Task<WorkspaceResponse> UpdateCurrentAsync(
        UpdateWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var workspace = await GetCurrentWorkspaceAsync(cancellationToken);

        if (request.Name is not null)
        {
            workspace.Rename(request.Name);
        }

        if (request.Color is not null)
        {
            workspace.ChangeColor(request.Color);
        }

        await _workspaceRepository.SaveChangesAsync(cancellationToken);

        return MapWorkspace(workspace);
    }

    private async Task<Workspace> GetCurrentWorkspaceAsync(CancellationToken cancellationToken)
    {
        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var workspace = await _workspaceRepository.GetByIdAsync(
            context.WorkspaceId,
            cancellationToken);

        return workspace ?? throw new InvalidOperationException("Development workspace was not found.");
    }

    private static WorkspaceResponse MapWorkspace(Workspace workspace)
    {
        return new WorkspaceResponse(
            workspace.Id,
            workspace.Name,
            workspace.Color,
            workspace.CreatedAt);
    }
}
