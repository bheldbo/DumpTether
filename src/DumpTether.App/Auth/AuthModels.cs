using DumpTether.Domain;

namespace DumpTether.App.Auth;

public sealed record RegisterUserRequest(
    string Email,
    string Password,
    string? DisplayName = null);

public sealed record LoginUserRequest(
    string Email,
    string Password,
    string? DeviceName = null);

public sealed record AuthRequestMetadata(
    string? UserAgent = null,
    string? IpAddress = null);

public sealed record AuthUserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset? EmailConfirmedAt);

public sealed record AuthWorkspaceResponse(
    Guid Id,
    string Name,
    string? Color,
    WorkspaceMembershipRole Role);

public sealed record RegisterUserResponse(
    AuthUserResponse User,
    AuthWorkspaceResponse Workspace,
    bool EmailConfirmationRequired);

public sealed record LoginUserResponse(
    AuthUserResponse User,
    IReadOnlyList<AuthWorkspaceResponse> Workspaces,
    string SessionToken,
    DateTimeOffset ExpiresAt);

public sealed record CurrentUserResponse(
    AuthUserResponse User,
    IReadOnlyList<AuthWorkspaceResponse> Workspaces);

public sealed record AuthClientOptionsResponse(
    bool RequiresAuthentication,
    bool GuestSessionsEnabled,
    bool DevelopmentLoginEnabled,
    bool EmailConfirmationEnabled,
    IReadOnlyList<string> OAuthProviders);

public sealed record ConfirmEmailResponse(
    Guid UserId,
    string Email,
    DateTimeOffset ConfirmedAt);

public sealed record ExternalLoginRequest(
    string Provider,
    string ProviderUserId,
    string Email,
    string? DisplayName = null);

public sealed record TestEmailRequest(
    string Email);

public sealed record CurrentUserSession(
    Guid UserId,
    Guid SessionId,
    string Email,
    string DisplayName);

public sealed record UserWorkspaceMembership(
    Workspace Workspace,
    WorkspaceMembership Membership);
