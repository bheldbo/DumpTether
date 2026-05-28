namespace DumpTether.App.Workspaces;

public interface IWorkspaceService
{
    Task<IReadOnlyList<WorkspaceResponse>> ListAsync(CancellationToken cancellationToken);

    Task<WorkspaceResponse> GetCurrentAsync(CancellationToken cancellationToken);

    Task<WorkspaceResponse> CreateAsync(
        CreateWorkspaceRequest request,
        CancellationToken cancellationToken);

    Task<WorkspaceResponse> UpdateCurrentAsync(
        UpdateWorkspaceRequest request,
        CancellationToken cancellationToken);
}
