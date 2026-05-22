namespace DumpTether.App.Workspaces;

public sealed record UpdateWorkspaceRequest(
    string? Name = null,
    string? Color = null);
