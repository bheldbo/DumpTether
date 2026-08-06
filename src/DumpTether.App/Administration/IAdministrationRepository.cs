using DumpTether.Domain;

namespace DumpTether.App.Administration;

public interface IAdministrationRepository
{
    Task<AdministrationStatistics> GetStatisticsAsync(
        DateTimeOffset now,
        DateTimeOffset recentlySeenSince,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AdministrationUserSummary>> ListUsersAsync(
        string? search,
        int limit,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<AdministrationUserDetails?> GetUserDetailsAsync(
        string normalizedEmail,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<AppUser?> GetUserForUpdateAsync(
        string normalizedEmail,
        CancellationToken cancellationToken);

    Task<int> RevokeSessionsAsync(
        Guid userId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken);

    Task AddAuditEventAsync(
        OperatorAuditEvent auditEvent,
        CancellationToken cancellationToken);

    Task<AccountDeletionResult> DeleteAccountAsync(
        AppUser user,
        OperatorAuditEvent auditEvent,
        DateTimeOffset deletedAt,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
