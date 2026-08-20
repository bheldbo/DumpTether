using DumpTether.App.Notifications;
using DumpTether.Domain;
using Microsoft.EntityFrameworkCore;

namespace DumpTether.Data;

internal sealed class EfUserNotificationRepository : IUserNotificationRepository
{
    private readonly DumpTetherDbContext _dbContext;

    public EfUserNotificationRepository(DumpTetherDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<UserNotificationPreference?> GetAsync(
        Guid userId,
        bool trackChanges,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.UserNotificationPreferences.AsQueryable();
        if (!trackChanges)
        {
            query = query.AsNoTracking();
        }

        return query.SingleOrDefaultAsync(
            preference => preference.UserId == userId,
            cancellationToken);
    }

    public async Task AddAsync(
        UserNotificationPreference preference,
        CancellationToken cancellationToken)
    {
        await _dbContext.UserNotificationPreferences.AddAsync(preference, cancellationToken);
    }

    public async Task<IReadOnlyList<UserNotificationPreference>> ListEnabledAsync(
        CancellationToken cancellationToken)
    {
        return await _dbContext.UserNotificationPreferences
            .AsNoTracking()
            .Where(preference =>
                preference.DailySummaryEmailEnabled ||
                preference.FollowUpReminderEmailEnabled)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> TryClaimAsync(
        Guid userId,
        NotificationDigestKind kind,
        DateTimeOffset scheduledFor,
        DateTimeOffset claimedAt,
        DateTimeOffset staleClaimBefore,
        CancellationToken cancellationToken)
    {
        var candidate = await GetAsync(userId, trackChanges: false, cancellationToken);
        if (candidate is null)
        {
            return false;
        }

        if (kind == NotificationDigestKind.DailySummary)
        {
            if (!candidate.DailySummaryEmailEnabled ||
                candidate.LastDailySummarySentAt >= scheduledFor ||
                candidate.DailySummaryClaimedAt > staleClaimBefore)
            {
                return false;
            }

            var expectedClaim = candidate.DailySummaryClaimedAt;
            var expectedSent = candidate.LastDailySummarySentAt;
            var affectedRows = await _dbContext.UserNotificationPreferences
                .Where(preference =>
                    preference.UserId == userId &&
                    preference.DailySummaryEmailEnabled &&
                    preference.DailySummaryClaimedAt == expectedClaim &&
                    preference.LastDailySummarySentAt == expectedSent)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        preference => preference.DailySummaryClaimedAt,
                        claimedAt),
                    cancellationToken);
            return affectedRows == 1;
        }

        if (!candidate.FollowUpReminderEmailEnabled ||
            candidate.LastFollowUpReminderSentAt >= scheduledFor ||
            candidate.FollowUpReminderClaimedAt > staleClaimBefore)
        {
            return false;
        }

        var followUpExpectedClaim = candidate.FollowUpReminderClaimedAt;
        var followUpExpectedSent = candidate.LastFollowUpReminderSentAt;
        var followUpAffectedRows = await _dbContext.UserNotificationPreferences
            .Where(preference =>
                preference.UserId == userId &&
                preference.FollowUpReminderEmailEnabled &&
                preference.FollowUpReminderClaimedAt == followUpExpectedClaim &&
                preference.LastFollowUpReminderSentAt == followUpExpectedSent)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    preference => preference.FollowUpReminderClaimedAt,
                    claimedAt),
                cancellationToken);
        return followUpAffectedRows == 1;
    }

    public async Task MarkSentAsync(
        Guid userId,
        NotificationDigestKind kind,
        DateTimeOffset claimedAt,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken)
    {
        if (kind == NotificationDigestKind.DailySummary)
        {
            await _dbContext.UserNotificationPreferences
                .Where(preference =>
                    preference.UserId == userId &&
                    preference.DailySummaryClaimedAt == claimedAt)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(preference => preference.LastDailySummarySentAt, sentAt)
                        .SetProperty(
                            preference => preference.DailySummaryClaimedAt,
                            (DateTimeOffset?)null),
                    cancellationToken);
            return;
        }

        await _dbContext.UserNotificationPreferences
            .Where(preference =>
                preference.UserId == userId &&
                preference.FollowUpReminderClaimedAt == claimedAt)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(preference => preference.LastFollowUpReminderSentAt, sentAt)
                    .SetProperty(
                        preference => preference.FollowUpReminderClaimedAt,
                        (DateTimeOffset?)null),
                cancellationToken);
    }

    public async Task ReleaseClaimAsync(
        Guid userId,
        NotificationDigestKind kind,
        DateTimeOffset claimedAt,
        CancellationToken cancellationToken)
    {
        if (kind == NotificationDigestKind.DailySummary)
        {
            await _dbContext.UserNotificationPreferences
                .Where(preference =>
                    preference.UserId == userId &&
                    preference.DailySummaryClaimedAt == claimedAt)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        preference => preference.DailySummaryClaimedAt,
                        (DateTimeOffset?)null),
                    cancellationToken);
            return;
        }

        await _dbContext.UserNotificationPreferences
            .Where(preference =>
                preference.UserId == userId &&
                preference.FollowUpReminderClaimedAt == claimedAt)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    preference => preference.FollowUpReminderClaimedAt,
                    (DateTimeOffset?)null),
                cancellationToken);
    }

    public async Task<NotificationDigestSnapshot?> GetDigestSnapshotAsync(
        Guid userId,
        DateTimeOffset updatedSince,
        DateTimeOffset now,
        DateTimeOffset followUpThrough,
        CancellationToken cancellationToken)
    {
        var user = await _dbContext.AppUsers
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == userId && candidate.IsActive, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var workspaceIds = _dbContext.WorkspaceMemberships
            .Where(membership => membership.UserId == userId)
            .Select(membership => membership.WorkspaceId);
        var sharedTaskIds = _dbContext.TaskItemShares
            .Where(share =>
                share.SharedWithUserId == userId &&
                share.RevokedAt == null &&
                (share.AcceptedAt != null || share.TokenHash == null))
            .Select(share => share.TaskItemId);
        var taskRows = await (
                from taskItem in _dbContext.TaskItems.AsNoTracking()
                join workspace in _dbContext.Workspaces.AsNoTracking()
                    on taskItem.WorkspaceId equals workspace.Id
                where taskItem.ArchivedAt == null &&
                    (workspaceIds.Contains(taskItem.WorkspaceId) ||
                        sharedTaskIds.Contains(taskItem.Id))
                select new
                {
                    taskItem.Title,
                    WorkspaceName = workspace.Name,
                    taskItem.LastTouchedAt,
                    taskItem.FollowUpAt
                })
            .ToListAsync(cancellationToken);

        // SQLite cannot translate ordering/comparisons for DateTimeOffset. Access is
        // still scoped in SQL; only this bounded per-user digest is evaluated here.
        var activeTaskCount = taskRows.Count;
        var updatedTaskCount = taskRows.Count(taskItem => taskItem.LastTouchedAt >= updatedSince);
        var overdueFollowUpCount = taskRows.Count(taskItem => taskItem.FollowUpAt < now);
        var followUps = taskRows
            .Where(taskItem =>
                taskItem.FollowUpAt.HasValue &&
                taskItem.FollowUpAt <= followUpThrough)
            .OrderBy(taskItem => taskItem.FollowUpAt)
            .Take(20)
            .Select(taskItem => new NotificationTaskDigestItem(
                taskItem.Title,
                taskItem.WorkspaceName,
                taskItem.FollowUpAt))
            .ToList();

        return new NotificationDigestSnapshot(
            user.Email,
            user.DisplayName,
            activeTaskCount,
            updatedTaskCount,
            overdueFollowUpCount,
            followUps);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}
