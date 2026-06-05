using DumpTether.Domain;

namespace DumpTether.App.Workspaces;

public interface IWorkspaceRepository
{
    Task<IReadOnlyList<Workspace>> ListAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Workspace>> ListForUserAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<WorkspaceMembership?> GetMembershipAsync(
        Guid workspaceId,
        Guid userId,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkspaceMember>> ListMembersAsync(
        Guid workspaceId,
        CancellationToken cancellationToken);

    Task AddMembershipAsync(
        WorkspaceMembership membership,
        CancellationToken cancellationToken);

    void RemoveMembership(WorkspaceMembership membership);

    Task<IReadOnlyList<WorkspaceInvitation>> ListInvitationsAsync(
        Guid workspaceId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkspaceInvitationInboxItem>> ListIncomingInvitationsAsync(
        string normalizedEmail,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<WorkspaceInvitation?> GetInvitationByIdAsync(
        Guid workspaceId,
        Guid invitationId,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task<WorkspaceInvitation?> GetIncomingInvitationByIdAsync(
        Guid invitationId,
        string normalizedEmail,
        DateTimeOffset now,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task<WorkspaceInvitation?> GetInvitationByTokenHashAsync(
        string tokenHash,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task<bool> HasUsableInvitationAsync(
        Guid workspaceId,
        string normalizedEmail,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task AddAsync(Workspace workspace, CancellationToken cancellationToken);

    Task AddInvitationAsync(
        WorkspaceInvitation invitation,
        CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid workspaceId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
