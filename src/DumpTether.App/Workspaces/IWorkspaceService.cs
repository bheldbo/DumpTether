namespace DumpTether.App.Workspaces;

public interface IWorkspaceService
{
    Task<WorkspaceResponse> GetCurrentAsync(CancellationToken cancellationToken);

    Task<WorkspaceResponse> UpdateCurrentAsync(
        UpdateWorkspaceRequest request,
        CancellationToken cancellationToken);
}
