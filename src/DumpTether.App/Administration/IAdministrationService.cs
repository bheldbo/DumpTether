namespace DumpTether.App.Administration;

public interface IAdministrationService
{
    Task<IReadOnlyList<AdministrationUserSummary>> ListUsersAsync(
        string? search,
        int limit,
        CancellationToken cancellationToken);

    Task<AdministrationUserDetails?> GetUserAsync(
        string email,
        CancellationToken cancellationToken);

    Task<bool> LockUserAsync(
        string email,
        string actor,
        string reason,
        CancellationToken cancellationToken);

    Task<bool> UnlockUserAsync(
        string email,
        string actor,
        string reason,
        CancellationToken cancellationToken);

    Task<int?> RevokeSessionsAsync(
        string email,
        string actor,
        string reason,
        CancellationToken cancellationToken);

    Task<AccountDeletionResult?> DeleteUserAsync(
        string email,
        string confirmationEmail,
        string actor,
        string reason,
        CancellationToken cancellationToken);
}
