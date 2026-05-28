namespace DumpTether.App.Projects;

public sealed record UpdateProjectRequest(
    string? Name = null,
    string? Color = null);

public sealed record CreateProjectRequest(
    string Name,
    string? Color = null);

public sealed record ArchiveProjectTasksRequest(
    Guid? ArchiveResolutionId,
    string? Note = null);
