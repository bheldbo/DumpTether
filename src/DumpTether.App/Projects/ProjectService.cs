using System.ComponentModel.DataAnnotations;
using DumpTether.App.Tasks;
using DumpTether.Domain;

namespace DumpTether.App.Projects;

internal sealed class ProjectService : IProjectService
{
    private readonly IClock _clock;
    private readonly IDevelopmentWorkspaceProvider _developmentWorkspaceProvider;
    private readonly IProjectRepository _projectRepository;
    private readonly ITaskItemRepository _taskItemRepository;

    public ProjectService(
        IClock clock,
        IDevelopmentWorkspaceProvider developmentWorkspaceProvider,
        IProjectRepository projectRepository,
        ITaskItemRepository taskItemRepository)
    {
        _clock = clock;
        _developmentWorkspaceProvider = developmentWorkspaceProvider;
        _projectRepository = projectRepository;
        _taskItemRepository = taskItemRepository;
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

    public async Task<ProjectResponse> CreateAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var project = Project.Create(context.WorkspaceId, request.Name, _clock.UtcNow);

        if (request.Color is not null)
        {
            project.ChangeColor(request.Color);
        }

        await _projectRepository.AddAsync(project, cancellationToken);
        await _projectRepository.SaveChangesAsync(cancellationToken);

        return MapProject(project);
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
            var now = _clock.UtcNow;
            project.Rename(request.Name);
            var projectTasks = await _taskItemRepository.ListByProjectAsync(
                context.WorkspaceId,
                project.Id,
                includeArchived: true,
                cancellationToken);

            foreach (var taskItem in projectTasks)
            {
                taskItem.ChangeCategory(project.Name, now);
            }
        }

        if (request.Color is not null)
        {
            project.ChangeColor(request.Color);
        }

        await _projectRepository.SaveChangesAsync(cancellationToken);

        return MapProject(project);
    }

    public async Task<ProjectArchiveResponse?> ArchiveTasksAndDeactivateAsync(
        Guid id,
        ArchiveProjectTasksRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.ArchiveResolutionId.HasValue ||
            request.ArchiveResolutionId.Value == Guid.Empty)
        {
            throw new ValidationException("ArchiveResolutionId is required.");
        }

        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var project = await _projectRepository.GetByIdAsync(
            id,
            context.WorkspaceId,
            cancellationToken);

        if (project is null)
        {
            return null;
        }

        var archiveResolution = await _taskItemRepository.GetArchiveResolutionByIdAsync(
            request.ArchiveResolutionId.Value,
            context.WorkspaceId,
            cancellationToken) ??
            throw new ValidationException("Archive resolution was not found.");
        var now = _clock.UtcNow;
        var taskItems = await _taskItemRepository.ListByProjectAsync(
            context.WorkspaceId,
            project.Id,
            includeArchived: false,
            cancellationToken);

        foreach (var taskItem in taskItems)
        {
            taskItem.Archive(archiveResolution, now, request.Note);
        }

        project.Deactivate();
        await _projectRepository.SaveChangesAsync(cancellationToken);

        return new ProjectArchiveResponse(project.Id, taskItems.Count);
    }

    private static ProjectResponse MapProject(Project project)
    {
        return new ProjectResponse(
            project.Id,
            project.WorkspaceId,
            project.Name,
            project.Color,
            project.CreatedAt,
            project.IsActive);
    }
}
