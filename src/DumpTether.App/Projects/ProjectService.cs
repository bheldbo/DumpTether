using DumpTether.App.Tasks;
using DumpTether.Domain;

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
            .Select(MapProject)
            .ToList();
    }

    public async Task<ProjectResponse?> UpdateAsync(
        Guid id,
        UpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var project = await _projectRepository.GetByIdAsync(
            id,
            context.WorkspaceId,
            cancellationToken);

        if (project is null)
        {
            return null;
        }

        if (request.Name is not null)
        {
            project.Rename(request.Name);
        }

        if (request.Color is not null)
        {
            project.ChangeColor(request.Color);
        }

        await _projectRepository.SaveChangesAsync(cancellationToken);

        return MapProject(project);
    }

    private static ProjectResponse MapProject(Project project)
    {
        return new ProjectResponse(
            project.Id,
            project.WorkspaceId,
            project.Name,
            project.Color,
            project.CreatedAt);
    }
}
