using DumpTether.Domain;

namespace DumpTether.App.Workspaces;

public sealed record WorkspaceMember(
    AppUser User,
    WorkspaceMembership Membership);

public sealed record WorkspaceInvitationInboxItem(
    WorkspaceInvitation Invitation,
    Workspace Workspace,
    AppUser InvitedByUser);
