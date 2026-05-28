namespace DumpTether.App.Projects;

public interface IProjectService
{
    Task<IReadOnlyList<ProjectResponse>> ListAsync(CancellationToken cancellationToken);

    Task<ProjectResponse> CreateAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken);

    Task<ProjectResponse?> UpdateAsync(
        Guid id,
        UpdateProjectRequest request,
        CancellationToken cancellationToken);

    Task<ProjectArchiveResponse?> ArchiveTasksAndDeactivateAsync(
        Guid id,
        ArchiveProjectTasksRequest request,
        CancellationToken cancellationToken);
}
