using DumpTether.App.Administration;
using DumpTether.App.Workspaces;
using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;

namespace DumpTether.Data;

internal sealed class EfAdministrationRepository : IAdministrationRepository
{
    private readonly DumpTetherDbContext _dbContext;
    private readonly IWorkspaceRepository _workspaceRepository;

    public EfAdministrationRepository(
        DumpTetherDbContext dbContext,
        IWorkspaceRepository workspaceRepository)
    {
        _dbContext = dbContext;
        _workspaceRepository = workspaceRepository;
    }

    public async Task<AdministrationStatistics> GetStatisticsAsync(
        DateTimeOffset now,
        DateTimeOffset recentlySeenSince,
        CancellationToken cancellationToken)
    {
        var registeredUsers = await _dbContext.AppUsers.CountAsync(cancellationToken);
        var activeUsers = await _dbContext.AppUsers.CountAsync(user => user.IsActive, cancellationToken);
        var confirmedUsers = await _dbContext.AppUsers.CountAsync(user => user.EmailConfirmedAt != null, cancellationToken);
        var unrevokedSessions = await _dbContext.UserSessions
            .AsNoTracking()
            .Where(session => session.RevokedAt == null)
            .Select(session => new { session.ExpiresAt, session.LastSeenAt })
            .ToListAsync(cancellationToken);
        var activeSessions = unrevokedSessions.Count(session => session.ExpiresAt > now);
        var recentlySeenSessions = unrevokedSessions.Count(session =>
            session.ExpiresAt > now && session.LastSeenAt >= recentlySeenSince);
        var boards = await _dbContext.Workspaces.CountAsync(cancellationToken);
        var activeTasks = await _dbContext.TaskItems.CountAsync(taskItem => taskItem.ArchivedAt == null, cancellationToken);
        var archivedTasks = await _dbContext.TaskItems.CountAsync(taskItem => taskItem.ArchivedAt != null, cancellationToken);

        return new AdministrationStatistics(
            registeredUsers,
            activeUsers,
            confirmedUsers,
            activeSessions,
            recentlySeenSessions,
            boards,
            activeTasks,
            archivedTasks,
            now);
    }

    public async Task<IReadOnlyList<AdministrationUserSummary>> ListUsersAsync(
        string? search,
        int limit,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.AppUsers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            var displaySearch = search.Trim().ToLowerInvariant();
            query = query.Where(user =>
                user.NormalizedEmail.Contains(normalizedSearch) ||
                user.DisplayName.ToLower().Contains(displaySearch));
        }

        var users = await query
            .OrderBy(user => user.NormalizedEmail)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return await BuildSummariesAsync(users, now, cancellationToken);
    }

    public async Task<AdministrationUserDetails?> GetUserDetailsAsync(
        string normalizedEmail,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.AppUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.NormalizedEmail == normalizedEmail,
                cancellationToken);

        if (user is null)
        {
            return null;
        }

        var summary = AssertSingle(await BuildSummariesAsync([user], now, cancellationToken));
        var storedSessions = await _dbContext.UserSessions
            .AsNoTracking()
            .Where(session => session.UserId == user.Id)
            .ToListAsync(cancellationToken);
        var sessions = storedSessions
            .OrderBy(session => session.RevokedAt == null ? 0 : 1)
            .ThenByDescending(session => session.LastSeenAt)
            .Take(50)
            .Select(session => new AdministrationSessionSummary(
                session.Id,
                session.SessionType,
                session.DeviceName,
                session.CreatedAt,
                session.ExpiresAt,
                session.LastSeenAt,
                session.RevokedAt))
            .ToList();

        return new AdministrationUserDetails(summary, sessions);
    }

    public Task<AppUser?> GetUserForUpdateAsync(
        string normalizedEmail,
        CancellationToken cancellationToken) =>
        _dbContext.AppUsers.SingleOrDefaultAsync(
            user => user.NormalizedEmail == normalizedEmail,
            cancellationToken);

    public async Task<int> RevokeSessionsAsync(
        Guid userId,
        DateTimeOffset revokedAt,
        CancellationToken cancellationToken)
    {
        var unrevokedSessions = await _dbContext.UserSessions
            .Where(session =>
                session.UserId == userId &&
                session.RevokedAt == null)
            .ToListAsync(cancellationToken);
        var sessions = unrevokedSessions
            .Where(session => session.ExpiresAt > revokedAt)
            .ToList();

        foreach (var session in sessions)
        {
            session.Revoke(revokedAt);
        }

        return sessions.Count;
    }

    public Task AddAuditEventAsync(
        OperatorAuditEvent auditEvent,
        CancellationToken cancellationToken) =>
        _dbContext.OperatorAuditEvents.AddAsync(auditEvent, cancellationToken).AsTask();

    public async Task<AccountDeletionResult> DeleteAccountAsync(
        AppUser user,
        OperatorAuditEvent auditEvent,
        DateTimeOffset deletedAt,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var ownedWorkspaceIds = await _dbContext.WorkspaceMemberships
                .Where(membership =>
                    membership.UserId == user.Id &&
                    membership.Role == WorkspaceMembershipRole.Owner)
                .Select(membership => membership.WorkspaceId)
                .ToListAsync(cancellationToken);

            foreach (var workspaceId in ownedWorkspaceIds)
            {
                await _workspaceRepository.DeleteAsync(workspaceId, cancellationToken);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            var sessions = await _dbContext.UserSessions
                .Where(session => session.UserId == user.Id)
                .ToListAsync(cancellationToken);
            var shares = await _dbContext.TaskItemShares
                .Where(share =>
                    share.SharedByUserId == user.Id ||
                    share.SharedWithUserId == user.Id ||
                    share.NormalizedEmail == user.NormalizedEmail)
                .ToListAsync(cancellationToken);

            _dbContext.UserSessions.RemoveRange(sessions);
            _dbContext.TaskItemShares.RemoveRange(shares);

            var templates = await _dbContext.TaskTemplates
                .Where(template => template.OwnerUserId == user.Id)
                .ToListAsync(cancellationToken);
            var templateIds = templates.Select(template => template.Id).ToList();
            var referencedTemplateIds = templateIds.Count == 0
                ? []
                : await _dbContext.TaskItems
                    .Where(taskItem =>
                        taskItem.TaskTemplateId.HasValue &&
                        templateIds.Contains(taskItem.TaskTemplateId.Value))
                    .Select(taskItem => taskItem.TaskTemplateId!.Value)
                    .Distinct()
                    .ToListAsync(cancellationToken);
            var referencedTemplateIdSet = referencedTemplateIds.ToHashSet();
            var preservedTemplates = templates
                .Where(template => referencedTemplateIdSet.Contains(template.Id))
                .ToList();
            var deletedTemplates = templates
                .Where(template => !referencedTemplateIdSet.Contains(template.Id))
                .ToList();

            foreach (var template in preservedTemplates)
            {
                template.ReleaseOwnership(deletedAt);
            }

            if (deletedTemplates.Count > 0)
            {
                var deletedTemplateIds = deletedTemplates.Select(template => template.Id).ToList();
                _dbContext.FieldDefinitions.RemoveRange(
                    await _dbContext.FieldDefinitions
                        .Where(field => deletedTemplateIds.Contains(field.TaskTemplateId))
                        .ToListAsync(cancellationToken));
                _dbContext.TaskTemplates.RemoveRange(deletedTemplates);
            }

            var remoteRoots = await _dbContext.SyncRoots
                .Where(root => root.CloudUserId == user.Id)
                .ToListAsync(cancellationToken);
            _dbContext.SyncRoots.RemoveRange(remoteRoots);

            _dbContext.AppUsers.Remove(user);
            await _dbContext.OperatorAuditEvents.AddAsync(auditEvent, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new AccountDeletionResult(
                user.Email,
                ownedWorkspaceIds.Count,
                sessions.Count,
                shares.Count,
                deletedTemplates.Count,
                preservedTemplates.Count);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _dbContext.SaveChangesAsync(cancellationToken);

    private async Task<IReadOnlyList<AdministrationUserSummary>> BuildSummariesAsync(
        IReadOnlyCollection<AppUser> users,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var userIds = users.Select(user => user.Id).ToList();
        var sessions = await _dbContext.UserSessions
            .AsNoTracking()
            .Where(session => userIds.Contains(session.UserId))
            .Select(session => new
            {
                session.UserId,
                session.RevokedAt,
                session.ExpiresAt
            })
            .ToListAsync(cancellationToken);
        var memberships = await _dbContext.WorkspaceMemberships
            .AsNoTracking()
            .Where(membership => userIds.Contains(membership.UserId))
            .Select(membership => new
            {
                membership.UserId,
                membership.Role
            })
            .ToListAsync(cancellationToken);

        return users
            .OrderByDescending(user => user.CreatedAt)
            .Select(user => new AdministrationUserSummary(
                user.Id,
                user.Email,
                user.DisplayName,
                user.IsActive,
                user.EmailConfirmedAt,
                user.CreatedAt,
                user.LastLoginAt,
                sessions.Count(session =>
                    session.UserId == user.Id &&
                    session.RevokedAt == null &&
                    session.ExpiresAt > now),
                memberships.Count(membership =>
                    membership.UserId == user.Id &&
                    membership.Role == WorkspaceMembershipRole.Owner),
                memberships.Count(membership => membership.UserId == user.Id)))
            .ToList();
    }

    private static T AssertSingle<T>(IReadOnlyList<T> values) =>
        values.Count == 1
            ? values[0]
            : throw new InvalidOperationException("Expected one administration record.");
}
