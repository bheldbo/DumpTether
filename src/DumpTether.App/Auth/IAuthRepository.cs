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

    Task<UserSession?> GetSessionByIdAsync(
        Guid id,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<UserSession>> ListSessionsForUserAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<EmailConfirmationToken?> GetEmailConfirmationTokenByHashAsync(
        string tokenHash,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task<PasswordResetToken?> GetPasswordResetTokenByHashAsync(
        string tokenHash,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task<AccountDeletionRequest?> GetAccountDeletionRequestForUserAsync(
        Guid userId,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AccountDeletionRequest>> ListAccountDeletionRemindersDueAsync(
        DateTimeOffset now,
        DateTimeOffset staleClaimBefore,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AccountDeletionRequest>> ListAccountDeletionsDueAsync(
        DateTimeOffset now,
        DateTimeOffset staleClaimBefore,
        CancellationToken cancellationToken);

    Task<bool> HasOwnedWorkspaceSharedWithOthersAsync(
        Guid userId,
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

    Task<int> DeleteInactiveAuthTokensAsync(
        DateTimeOffset now,
        DateTimeOffset deleteBefore,
        CancellationToken cancellationToken);

    Task AddEmailConfirmationTokenAsync(
        EmailConfirmationToken token,
        CancellationToken cancellationToken);

    Task AddPasswordResetTokenAsync(
        PasswordResetToken token,
        CancellationToken cancellationToken);

    Task AddOperatorAuditEventAsync(
        OperatorAuditEvent auditEvent,
        CancellationToken cancellationToken);

    Task<bool> TryConsumePasswordResetTokenAsync(
        Guid tokenId,
        DateTimeOffset usedAt,
        CancellationToken cancellationToken);

    Task<int> InvalidatePasswordResetTokensForUserAsync(
        Guid userId,
        Guid exceptTokenId,
        DateTimeOffset invalidatedAt,
        CancellationToken cancellationToken);

    Task AddAccountDeletionRequestAsync(
        AccountDeletionRequest request,
        CancellationToken cancellationToken);

    void RemoveAccountDeletionRequest(AccountDeletionRequest request);

    Task<bool> TryClaimAccountDeletionReminderAsync(
        Guid requestId,
        DateTimeOffset claimedAt,
        DateTimeOffset staleClaimBefore,
        CancellationToken cancellationToken);

    Task MarkAccountDeletionReminderSentAsync(
        Guid requestId,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken);

    Task ReleaseAccountDeletionReminderClaimAsync(
        Guid requestId,
        CancellationToken cancellationToken);

    Task<bool> TryClaimAccountDeletionAsync(
        Guid requestId,
        DateTimeOffset claimedAt,
        DateTimeOffset staleClaimBefore,
        CancellationToken cancellationToken);

    Task ReleaseAccountDeletionClaimAsync(
        Guid requestId,
        CancellationToken cancellationToken);

    Task<int> RevokeSessionsForUserAsync(
        Guid userId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken);

    Task AddExternalLoginAsync(ExternalLogin externalLogin, CancellationToken cancellationToken);

    Task AddLegalAcceptancesAsync(
        IReadOnlyCollection<LegalAcceptance> acceptances,
        CancellationToken cancellationToken);

    Task AddWorkspaceMembershipAsync(
        WorkspaceMembership membership,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
