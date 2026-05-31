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

    Task<IReadOnlyList<UserWorkspaceMembership>> ListWorkspacesForUserAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task AddUserAsync(AppUser user, CancellationToken cancellationToken);

    Task AddSessionAsync(UserSession session, CancellationToken cancellationToken);

    Task AddWorkspaceMembershipAsync(
        WorkspaceMembership membership,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
