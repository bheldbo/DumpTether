namespace DumpTether.App.Projects;

public sealed record ProjectResponse(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string? Color,
    DateTimeOffset CreatedAt);
