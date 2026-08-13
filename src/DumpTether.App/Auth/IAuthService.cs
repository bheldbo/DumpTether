namespace DumpTether.App.Auth;

public interface IAuthService
{
    Task<RegisterUserResponse> RegisterAsync(
        RegisterUserRequest request,
        CancellationToken cancellationToken);

    Task<LoginUserResponse> LoginAsync(
        LoginUserRequest request,
        AuthRequestMetadata metadata,
        CancellationToken cancellationToken);

    Task<LoginUserResponse> DesktopCloudLoginAsync(
        LoginUserRequest request,
        AuthRequestMetadata metadata,
        CancellationToken cancellationToken);

    Task<LoginUserResponse> DevelopmentLoginAsync(
        AuthRequestMetadata metadata,
        CancellationToken cancellationToken);

    Task<LoginUserResponse> LocalDesktopLoginAsync(
        AuthRequestMetadata metadata,
        CancellationToken cancellationToken);

    Task<LoginUserResponse> GuestLoginAsync(
        AuthRequestMetadata metadata,
        CancellationToken cancellationToken);

    Task<LoginUserResponse> ExternalLoginAsync(
        ExternalLoginRequest request,
        AuthRequestMetadata metadata,
        CancellationToken cancellationToken);

    Task<ConfirmEmailResponse> ConfirmEmailAsync(
        string token,
        CancellationToken cancellationToken);

    Task RequestPasswordResetAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken);

    Task ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken);

    Task SendPasswordResetForOperatorAsync(
        string email,
        string actor,
        string reason,
        CancellationToken cancellationToken);

    Task<AccountDeletionStatusResponse?> GetAccountDeletionStatusAsync(
        CancellationToken cancellationToken);

    Task<AccountDeletionStatusResponse> RequestAccountDeletionAsync(
        RequestAccountDeletionRequest request,
        CancellationToken cancellationToken);

    Task<bool> CancelAccountDeletionAsync(CancellationToken cancellationToken);

    Task<bool> LogoutAsync(CancellationToken cancellationToken);

    Task<CurrentUserResponse?> GetCurrentAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<AuthSessionListItemResponse>> ListSessionsAsync(
        CancellationToken cancellationToken);

    Task<RevokeAuthSessionResponse> RevokeSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken);
}
