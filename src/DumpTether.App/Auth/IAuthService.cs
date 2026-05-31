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

    Task<LoginUserResponse> DevelopmentLoginAsync(
        AuthRequestMetadata metadata,
        CancellationToken cancellationToken);

    Task<bool> LogoutAsync(CancellationToken cancellationToken);

    Task<CurrentUserResponse?> GetCurrentAsync(CancellationToken cancellationToken);
}
