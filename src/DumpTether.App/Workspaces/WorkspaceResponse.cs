using DumpTether.Domain;

namespace DumpTether.App.Workspaces;

public sealed record WorkspaceResponse(
    Guid Id,
    string Name,
    string? Color,
    DateTimeOffset CreatedAt,
    string AccessKind = "Membership",
    int SharedTaskCount = 0,
    int MemberCount = 1,
    int PendingInvitationCount = 0);

public sealed record WorkspaceMemberResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    WorkspaceMembershipRole Role,
    DateTimeOffset CreatedAt);

public sealed record WorkspaceInvitationResponse(
    Guid Id,
    Guid WorkspaceId,
    string Email,
    WorkspaceMembershipRole Role,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? RevokedAt,
    string? Token = null);

public sealed record WorkspaceInvitationInboxResponse(
    Guid Id,
    Guid WorkspaceId,
    string WorkspaceName,
    string? WorkspaceColor,
    string InvitedByEmail,
    string InvitedByDisplayName,
    WorkspaceMembershipRole Role,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);
