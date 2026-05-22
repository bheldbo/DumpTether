namespace DumpTether.App.Workspaces;

public sealed record WorkspaceResponse(
    Guid Id,
    string Name,
    string? Color,
    DateTimeOffset CreatedAt);
