using DumpTether.Domain;

namespace DumpTether.App.Auth;

public interface IAuthRepository
{
    Task<AppUser?> GetUserByNormalizedEmailAsync(
        string normalizedEmail,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task<AppUser?> GetUserByIdAsync(
        Guid id,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task<UserSession?> GetSessionByTokenHashAsync(
        string sessionTokenHash,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task<EmailConfirmationToken?> GetEmailConfirmationTokenByHashAsync(
        string tokenHash,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task<ExternalLogin?> GetExternalLoginAsync(
        string provider,
        string providerUserId,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<UserWorkspaceMembership>> ListWorkspacesForUserAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task AddUserAsync(AppUser user, CancellationToken cancellationToken);

    Task AddSessionAsync(UserSession session, CancellationToken cancellationToken);

    Task<int> DeleteInactiveSessionsAsync(
        DateTimeOffset now,
        DateTimeOffset deleteBefore,
        CancellationToken cancellationToken);

    Task AddEmailConfirmationTokenAsync(
        EmailConfirmationToken token,
        CancellationToken cancellationToken);

    Task AddExternalLoginAsync(ExternalLogin externalLogin, CancellationToken cancellationToken);

    Task AddWorkspaceMembershipAsync(
        WorkspaceMembership membership,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
