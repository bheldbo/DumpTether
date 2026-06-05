namespace DumpTether.App.Tasks;

public sealed record DevelopmentWorkspaceContext(
    Guid WorkspaceId,
    Guid ProjectId,
    bool IsSharedOnly = false);
