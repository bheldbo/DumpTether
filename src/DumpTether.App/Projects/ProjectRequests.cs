namespace DumpTether.App.Projects;

public sealed record UpdateProjectRequest(
    string? Name = null,
    string? Color = null);
