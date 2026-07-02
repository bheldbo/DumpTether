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

    public async Task AddEmailConfirmationTokenAsync(
        EmailConfirmationToken token,
        CancellationToken cancellationToken)
    {
        await _dbContext.EmailConfirmationTokens.AddAsync(token, cancellationToken);
    }

    public async Task AddExternalLoginAsync(
        ExternalLogin externalLogin,
        CancellationToken cancellationToken)
    {
        await _dbContext.ExternalLogins.AddAsync(externalLogin, cancellationToken);
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
