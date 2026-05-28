namespace DumpTether.App.Workspaces;

public interface ICurrentWorkspaceSelection
{
    Guid? WorkspaceId { get; }
}
