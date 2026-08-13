using DumpTether.App.Auth;
using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;

namespace DumpTether.Data;

internal sealed class EfAuthRepository : IAuthRepository
{
    private readonly DumpTetherDbContext _dbContext;

    public EfAuthRepository(DumpTetherDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AppUser?> GetUserByNormalizedEmailAsync(
        string normalizedEmail,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.AppUsers
            .Where(user => user.NormalizedEmail == normalizedEmail);

        if (!trackChanges)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<AppUser?> GetUserByIdAsync(
        Guid id,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.AppUsers
            .Where(user => user.Id == id);

        if (!trackChanges)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<UserSession?> GetSessionByTokenHashAsync(
        string sessionTokenHash,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.UserSessions
            .Where(session => session.SessionTokenHash == sessionTokenHash);

        if (!trackChanges)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<UserSession?> GetSessionByIdAsync(
        Guid id,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.UserSessions
            .Where(session => session.Id == id);

        if (!trackChanges)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserSession>> ListSessionsForUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var sessions = await _dbContext.UserSessions
            .AsNoTracking()
            .Where(session => session.UserId == userId)
            .ToListAsync(cancellationToken);

        return sessions
            .OrderBy(session => session.RevokedAt == null ? 0 : 1)
            .ThenByDescending(session => session.LastSeenAt)
            .ThenByDescending(session => session.CreatedAt)
            .Take(50)
            .ToList();
    }

    public async Task<EmailConfirmationToken?> GetEmailConfirmationTokenByHashAsync(
        string tokenHash,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.EmailConfirmationTokens
            .Where(token => token.TokenHash == tokenHash);

        if (!trackChanges)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<PasswordResetToken?> GetPasswordResetTokenByHashAsync(
        string tokenHash,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.PasswordResetTokens
            .Where(token => token.TokenHash == tokenHash);
        if (!trackChanges)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<AccountDeletionRequest?> GetAccountDeletionRequestForUserAsync(
        Guid userId,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.AccountDeletionRequests.Where(request => request.UserId == userId);
        if (!trackChanges)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AccountDeletionRequest>> ListAccountDeletionRemindersDueAsync(
        DateTimeOffset now,
        DateTimeOffset staleClaimBefore,
        CancellationToken cancellationToken)
    {
        var candidates = await _dbContext.AccountDeletionRequests
            .AsNoTracking()
            .Where(request =>
                request.State == AccountDeletionRequestState.Pending &&
                request.ReminderSentAt == null)
            .ToListAsync(cancellationToken);
        return candidates
            .Where(request =>
                request.ReminderDueAt <= now &&
                (request.ReminderClaimedAt is null || request.ReminderClaimedAt <= staleClaimBefore))
            .OrderBy(request => request.ReminderDueAt)
            .Take(100)
            .ToList();
    }

    public async Task<IReadOnlyList<AccountDeletionRequest>> ListAccountDeletionsDueAsync(
        DateTimeOffset now,
        DateTimeOffset staleClaimBefore,
        CancellationToken cancellationToken)
    {
        var candidates = await _dbContext.AccountDeletionRequests
            .AsNoTracking()
            .Where(request => request.State == AccountDeletionRequestState.Pending ||
                request.State == AccountDeletionRequestState.Deleting)
            .ToListAsync(cancellationToken);
        return candidates
            .Where(request =>
                request.ScheduledFor <= now &&
                (request.State == AccountDeletionRequestState.Pending ||
                    request.ClaimedAt <= staleClaimBefore))
            .OrderBy(request => request.ScheduledFor)
            .Take(100)
            .ToList();
    }

    public async Task<bool> HasOwnedWorkspaceSharedWithOthersAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var ownedWorkspaceIds = _dbContext.WorkspaceMemberships
            .Where(membership =>
                membership.UserId == userId &&
                membership.Role == WorkspaceMembershipRole.Owner)
            .Select(membership => membership.WorkspaceId);

        var hasMembers = await _dbContext.WorkspaceMemberships
            .AnyAsync(membership =>
                membership.UserId != userId &&
                ownedWorkspaceIds.Contains(membership.WorkspaceId),
                cancellationToken);
        if (hasMembers)
        {
            return true;
        }

        var hasInvitations = await _dbContext.WorkspaceInvitations
            .AnyAsync(invitation =>
                invitation.RevokedAt == null &&
                invitation.AcceptedAt == null &&
                ownedWorkspaceIds.Contains(invitation.WorkspaceId),
                cancellationToken);
        if (hasInvitations)
        {
            return true;
        }

        return await _dbContext.TaskItemShares
            .AnyAsync(share =>
                share.RevokedAt == null &&
                ownedWorkspaceIds.Contains(share.WorkspaceId),
                cancellationToken);
    }

    public async Task<ExternalLogin?> GetExternalLoginAsync(
        string provider,
        string providerUserId,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var normalizedProvider = ExternalLogin.NormalizeProvider(provider);
        var query = _dbContext.ExternalLogins
            .Where(login =>
                login.Provider == normalizedProvider &&
                login.ProviderUserId == providerUserId);

        if (!trackChanges)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserWorkspaceMembership>> ListWorkspacesForUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.AppUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        if (user is null)
        {
            return [];
        }

        var memberships = await _dbContext.WorkspaceMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == userId)
            .Join(
                _dbContext.Workspaces.AsNoTracking(),
                membership => membership.WorkspaceId,
                workspace => workspace.Id,
                (membership, workspace) => new { membership, workspace })
            .ToListAsync(cancellationToken);

        var sharedWorkspaceIds = await _dbContext.TaskItemShares
            .AsNoTracking()
            .Where(share =>
                share.RevokedAt == null &&
                (share.AcceptedAt != null || share.TokenHash == null) &&
                (share.SharedWithUserId == userId ||
                    share.NormalizedEmail == user.NormalizedEmail))
            .Select(share => share.WorkspaceId)
            .ToListAsync(cancellationToken);
        var sharedTaskCounts = sharedWorkspaceIds
            .GroupBy(workspaceId => workspaceId)
            .ToDictionary(group => group.Key, group => group.Count());
        var sharedWorkspaceKeys = sharedTaskCounts.Keys.ToList();
        List<Workspace> sharedWorkspaces = sharedWorkspaceKeys.Count == 0
            ? []
            : await _dbContext.Workspaces
                .AsNoTracking()
                .Where(workspace => sharedWorkspaceKeys.Contains(workspace.Id))
                .ToListAsync(cancellationToken);

        var workspaceMemberships = memberships
            .OrderBy(item => item.membership.CreatedAt)
            .ThenBy(item => item.workspace.Name)
            .Select(item => new UserWorkspaceMembership(item.workspace, item.membership));
        var sharedWorkspaceMemberships = sharedWorkspaces
            .Where(workspace => memberships.All(item => item.workspace.Id != workspace.Id))
            .OrderBy(workspace => workspace.Name)
            .Select(workspace => new UserWorkspaceMembership(
                workspace,
                WorkspaceMembership.Create(
                    workspace.Id,
                    userId,
                    WorkspaceMembershipRole.Member,
                    DateTimeOffset.MinValue),
                WorkspaceAccessKinds.TaskShare,
                sharedTaskCounts.GetValueOrDefault(workspace.Id)));

        return workspaceMemberships
            .Concat(sharedWorkspaceMemberships)
            .ToList();
    }

    public async Task AddUserAsync(AppUser user, CancellationToken cancellationToken)
    {
        await _dbContext.AppUsers.AddAsync(user, cancellationToken);
    }

    public async Task AddSessionAsync(UserSession session, CancellationToken cancellationToken)
    {
        await _dbContext.UserSessions.AddAsync(session, cancellationToken);
    }

    public async Task<int> DeleteInactiveSessionsAsync(
        DateTimeOffset now,
        DateTimeOffset deleteBefore,
        CancellationToken cancellationToken)
    {
        var sessions = await _dbContext.UserSessions.ToListAsync(cancellationToken);
        var inactiveSessions = sessions
            .Where(session =>
                (session.ExpiresAt <= now && session.ExpiresAt <= deleteBefore) ||
                (session.RevokedAt.HasValue && session.RevokedAt <= deleteBefore))
            .ToList();

        _dbContext.UserSessions.RemoveRange(inactiveSessions);
        return inactiveSessions.Count;
    }

    public async Task<int> DeleteInactiveAuthTokensAsync(
        DateTimeOffset now,
        DateTimeOffset deleteBefore,
        CancellationToken cancellationToken)
    {
        var confirmationTokens = await _dbContext.EmailConfirmationTokens
            .ToListAsync(cancellationToken);
        var inactiveConfirmationTokens = confirmationTokens
            .Where(token =>
                token.ExpiresAt <= now ||
                (token.UsedAt.HasValue && token.UsedAt <= deleteBefore))
            .ToList();

        var passwordResetTokens = await _dbContext.PasswordResetTokens
            .ToListAsync(cancellationToken);
        var inactivePasswordResetTokens = passwordResetTokens
            .Where(token =>
                token.ExpiresAt <= now ||
                (token.UsedAt.HasValue && token.UsedAt <= deleteBefore))
            .ToList();

        _dbContext.EmailConfirmationTokens.RemoveRange(inactiveConfirmationTokens);
        _dbContext.PasswordResetTokens.RemoveRange(inactivePasswordResetTokens);
        return inactiveConfirmationTokens.Count + inactivePasswordResetTokens.Count;
    }

    public async Task AddEmailConfirmationTokenAsync(
        EmailConfirmationToken token,
        CancellationToken cancellationToken)
    {
        await _dbContext.EmailConfirmationTokens.AddAsync(token, cancellationToken);
    }

    public async Task AddPasswordResetTokenAsync(
        PasswordResetToken token,
        CancellationToken cancellationToken)
    {
        await _dbContext.PasswordResetTokens.AddAsync(token, cancellationToken);
    }

    public async Task AddOperatorAuditEventAsync(
        OperatorAuditEvent auditEvent,
        CancellationToken cancellationToken)
    {
        await _dbContext.OperatorAuditEvents.AddAsync(auditEvent, cancellationToken);
    }

    public async Task<bool> TryConsumePasswordResetTokenAsync(
        Guid tokenId,
        DateTimeOffset usedAt,
        CancellationToken cancellationToken)
    {
        var affectedRows = await _dbContext.PasswordResetTokens
            .Where(token =>
                token.Id == tokenId &&
                token.UsedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.UsedAt, usedAt),
                cancellationToken);
        return affectedRows == 1;
    }

    public Task<int> InvalidatePasswordResetTokensForUserAsync(
        Guid userId,
        Guid exceptTokenId,
        DateTimeOffset invalidatedAt,
        CancellationToken cancellationToken) =>
        _dbContext.PasswordResetTokens
            .Where(token =>
                token.UserId == userId &&
                token.Id != exceptTokenId &&
                token.UsedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.UsedAt, invalidatedAt),
                cancellationToken);

    public async Task AddAccountDeletionRequestAsync(
        AccountDeletionRequest request,
        CancellationToken cancellationToken)
    {
        await _dbContext.AccountDeletionRequests.AddAsync(request, cancellationToken);
    }

    public void RemoveAccountDeletionRequest(AccountDeletionRequest request)
    {
        _dbContext.AccountDeletionRequests.Remove(request);
    }

    public async Task<bool> TryClaimAccountDeletionReminderAsync(
        Guid requestId,
        DateTimeOffset claimedAt,
        DateTimeOffset staleClaimBefore,
        CancellationToken cancellationToken)
    {
        var candidate = await _dbContext.AccountDeletionRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(
                request =>
                    request.Id == requestId &&
                    request.State == AccountDeletionRequestState.Pending &&
                    request.ReminderSentAt == null,
                cancellationToken);
        if (candidate is null ||
            candidate.ReminderDueAt > claimedAt ||
            (candidate.ReminderClaimedAt is not null &&
                candidate.ReminderClaimedAt > staleClaimBefore))
        {
            return false;
        }

        var expectedClaim = candidate.ReminderClaimedAt;
        var affectedRows = await _dbContext.AccountDeletionRequests
            .Where(request =>
                request.Id == requestId &&
                request.State == AccountDeletionRequestState.Pending &&
                request.ReminderSentAt == null &&
                request.ReminderClaimedAt == expectedClaim)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(request => request.ReminderClaimedAt, claimedAt),
                cancellationToken);
        return affectedRows == 1;
    }

    public async Task MarkAccountDeletionReminderSentAsync(
        Guid requestId,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken)
    {
        await _dbContext.AccountDeletionRequests
            .Where(request => request.Id == requestId && request.ReminderSentAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(request => request.ReminderSentAt, sentAt)
                    .SetProperty(request => request.ReminderClaimedAt, (DateTimeOffset?)null),
                cancellationToken);
    }

    public async Task ReleaseAccountDeletionReminderClaimAsync(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        await _dbContext.AccountDeletionRequests
            .Where(request => request.Id == requestId && request.ReminderSentAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(request => request.ReminderClaimedAt, (DateTimeOffset?)null),
                cancellationToken);
    }

    public async Task<bool> TryClaimAccountDeletionAsync(
        Guid requestId,
        DateTimeOffset claimedAt,
        DateTimeOffset staleClaimBefore,
        CancellationToken cancellationToken)
    {
        var candidate = await _dbContext.AccountDeletionRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(request => request.Id == requestId, cancellationToken);
        if (candidate is null ||
            candidate.ScheduledFor > claimedAt ||
            (candidate.State != AccountDeletionRequestState.Pending &&
                (candidate.State != AccountDeletionRequestState.Deleting ||
                    candidate.ClaimedAt > staleClaimBefore)))
        {
            return false;
        }

        var expectedState = candidate.State;
        var expectedClaim = candidate.ClaimedAt;
        var affectedRows = await _dbContext.AccountDeletionRequests
            .Where(request =>
                request.Id == requestId &&
                request.State == expectedState &&
                request.ClaimedAt == expectedClaim)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(request => request.State, AccountDeletionRequestState.Deleting)
                    .SetProperty(request => request.ClaimedAt, claimedAt),
                cancellationToken);
        return affectedRows == 1;
    }

    public async Task ReleaseAccountDeletionClaimAsync(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        await _dbContext.AccountDeletionRequests
            .Where(request =>
                request.Id == requestId &&
                request.State == AccountDeletionRequestState.Deleting)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(request => request.State, AccountDeletionRequestState.Pending)
                    .SetProperty(request => request.ClaimedAt, (DateTimeOffset?)null),
                cancellationToken);
    }

    public async Task<int> RevokeSessionsForUserAsync(
        Guid userId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken)
    {
        var sessions = await _dbContext.UserSessions
            .Where(session =>
                session.UserId == userId &&
                session.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var session in sessions)
        {
            session.Revoke(revokedAt);
        }

        return sessions.Count;
    }

    public async Task AddExternalLoginAsync(
        ExternalLogin externalLogin,
        CancellationToken cancellationToken)
    {
        await _dbContext.ExternalLogins.AddAsync(externalLogin, cancellationToken);
    }

    public async Task AddLegalAcceptancesAsync(
        IReadOnlyCollection<LegalAcceptance> acceptances,
        CancellationToken cancellationToken)
    {
        await _dbContext.LegalAcceptances.AddRangeAsync(acceptances, cancellationToken);
    }

    public async Task AddWorkspaceMembershipAsync(
        WorkspaceMembership membership,
        CancellationToken cancellationToken)
    {
        await _dbContext.WorkspaceMemberships.AddAsync(membership, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
