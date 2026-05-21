using DumpTether.App.Tasks;

namespace DumpTether.App.Projects;

internal sealed class ProjectService : IProjectService
{
    private readonly IDevelopmentWorkspaceProvider _developmentWorkspaceProvider;
    private readonly IProjectRepository _projectRepository;

    public ProjectService(
        IDevelopmentWorkspaceProvider developmentWorkspaceProvider,
        IProjectRepository projectRepository)
    {
        _developmentWorkspaceProvider = developmentWorkspaceProvider;
        _projectRepository = projectRepository;
    }

    public async Task<IReadOnlyList<ProjectResponse>> ListAsync(
        CancellationToken cancellationToken)
    {
        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var projects = await _projectRepository.ListAsync(
            context.WorkspaceId,
            cancellationToken);

        return projects
            .Select(project => new ProjectResponse(
                project.Id,
                project.WorkspaceId,
                project.Name,
                project.CreatedAt))
            .ToList();
    }
}
