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
        await EnsureProjectNameIsAvailableAsync(
            context.WorkspaceId,
            request.Name,
            exceptProjectId: null,
            cancellationToken);
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
            var previousName = project.Name;
            await EnsureProjectNameIsAvailableAsync(
                context.WorkspaceId,
                request.Name,
                project.Id,
                cancellationToken);
            var now = _clock.UtcNow;
            project.Rename(request.Name);
            var projectTasks = await ListTasksUsingProjectOrCategoryAsync(
                context.WorkspaceId,
                project.Id,
                previousName,
                cancellationToken);

            foreach (var taskItem in projectTasks)
            {
                taskItem.ChangeCategory(
                    ReplaceCategory(taskItem.Category, previousName, project.Name),
                    now);
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

    public async Task<ProjectArchiveResponse?> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var context = await _developmentWorkspaceProvider.GetCurrentAsync(cancellationToken);
        var project = await _projectRepository.GetByIdAsync(
            id,
            context.WorkspaceId,
            cancellationToken);

        if (project is null)
        {
            return null;
        }

        var now = _clock.UtcNow;
        var taskItems = await ListTasksUsingProjectOrCategoryAsync(
            context.WorkspaceId,
            project.Id,
            project.Name,
            cancellationToken);

        foreach (var taskItem in taskItems)
        {
            if (taskItem.ProjectId == project.Id)
            {
                taskItem.AssignProject(null);
            }

            taskItem.ChangeCategory(
                RemoveCategory(taskItem.Category, project.Name),
                now);
        }

        project.Deactivate();
        await _projectRepository.SaveChangesAsync(cancellationToken);

        return new ProjectArchiveResponse(project.Id, taskItems.Count);
    }

    private async Task EnsureProjectNameIsAvailableAsync(
        Guid workspaceId,
        string name,
        Guid? exceptProjectId,
        CancellationToken cancellationToken)
    {
        var normalizedName = name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return;
        }

        var existingProjects = await _projectRepository.ListAsync(
            workspaceId,
            cancellationToken);
        var nameIsTaken = existingProjects.Any(project =>
            project.Id != exceptProjectId &&
            string.Equals(project.Name.Trim(), normalizedName, StringComparison.OrdinalIgnoreCase));

        if (nameIsTaken)
        {
            throw new ValidationException("A category with that name already exists.");
        }
    }

    private async Task<IReadOnlyList<TaskItem>> ListTasksUsingProjectOrCategoryAsync(
        Guid workspaceId,
        Guid projectId,
        string category,
        CancellationToken cancellationToken)
    {
        var byProject = await _taskItemRepository.ListByProjectAsync(
            workspaceId,
            projectId,
            includeArchived: true,
            cancellationToken);
        var byCategory = await _taskItemRepository.ListByCategoryAsync(
            workspaceId,
            category,
            includeArchived: true,
            cancellationToken);

        return byProject
            .Concat(byCategory)
            .GroupBy(taskItem => taskItem.Id)
            .Select(group => group.First())
            .ToList();
    }

    private static string? ReplaceCategory(
        string? categories,
        string previousCategory,
        string nextCategory)
    {
        var replaced = SplitCategories(categories)
            .Select(category => string.Equals(
                category,
                previousCategory,
                StringComparison.OrdinalIgnoreCase)
                ? nextCategory.Trim()
                : category)
            .ToArray();

        return JoinCategories(replaced);
    }

    private static string? RemoveCategory(string? categories, string removedCategory)
    {
        var remaining = SplitCategories(categories)
            .Where(category => !string.Equals(
                category,
                removedCategory,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return JoinCategories(remaining);
    }

    private static IReadOnlyList<string> SplitCategories(string? categories)
    {
        if (string.IsNullOrWhiteSpace(categories))
        {
            return [];
        }

        return categories
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? JoinCategories(IEnumerable<string> categories)
    {
        var normalized = categories
            .Select(category => category.Trim())
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalized.Length == 0 ? null : string.Join("; ", normalized);
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
