using DumpTether.Domain;

namespace DumpTether.App.Tasks;

public sealed record DevelopmentWorkspaceContext(
    Guid WorkspaceId,
    Guid ProjectId,
    bool IsSharedOnly = false,
    WorkspaceMembershipRole? MembershipRole = null)
{
    public bool CanWriteWorkspace =>
        !IsSharedOnly && MembershipRole != WorkspaceMembershipRole.ReadOnly;
}
