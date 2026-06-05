namespace DumpTether.App.Workspaces;

public interface IWorkspaceService
{
    Task<IReadOnlyList<WorkspaceResponse>> ListAsync(CancellationToken cancellationToken);

    Task<WorkspaceResponse> GetCurrentAsync(CancellationToken cancellationToken);

    Task<WorkspaceResponse> CreateAsync(
        CreateWorkspaceRequest request,
        CancellationToken cancellationToken);

    Task<WorkspaceResponse> UpdateCurrentAsync(
        UpdateWorkspaceRequest request,
        CancellationToken cancellationToken);

    Task<WorkspaceResponse?> UpdateAsync(
        Guid workspaceId,
        UpdateWorkspaceRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkspaceMemberResponse>> ListMembersAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkspaceInvitationResponse>> ListInvitationsAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkspaceInvitationInboxResponse>> ListIncomingInvitationsAsync(
        CancellationToken cancellationToken);

    Task<WorkspaceInvitationResponse> CreateInvitationAsync(
        CreateWorkspaceInvitationRequest request,
        CancellationToken cancellationToken);

    Task<WorkspaceInvitationResponse> AcceptInvitationAsync(
        AcceptWorkspaceInvitationRequest request,
        CancellationToken cancellationToken);

    Task<WorkspaceInvitationResponse> AcceptInvitationTokenAsync(
        string token,
        CancellationToken cancellationToken);

    Task<WorkspaceInvitationResponse> AcceptIncomingInvitationAsync(
        Guid invitationId,
        CancellationToken cancellationToken);

    Task<bool> DeclineIncomingInvitationAsync(
        Guid invitationId,
        CancellationToken cancellationToken);

    Task<bool> RevokeInvitationAsync(
        Guid invitationId,
        CancellationToken cancellationToken);

    Task<bool> RemoveMemberAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<bool> LeaveCurrentWorkspaceAsync(CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid workspaceId, CancellationToken cancellationToken);
}
