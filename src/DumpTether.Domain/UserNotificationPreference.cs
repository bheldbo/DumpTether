namespace DumpTether.Domain;

public sealed class UserNotificationPreference
{
    private UserNotificationPreference()
    {
    }

    private UserNotificationPreference(Guid userId, DateTimeOffset createdAt)
    {
        UserId = userId;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid UserId { get; private set; }

    public bool SharingActivityEmailEnabled { get; private set; }

    public bool DailySummaryEmailEnabled { get; private set; }

    public bool FollowUpReminderEmailEnabled { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DailySummaryClaimedAt { get; private set; }

    public DateTimeOffset? LastDailySummarySentAt { get; private set; }

    public DateTimeOffset? FollowUpReminderClaimedAt { get; private set; }

    public DateTimeOffset? LastFollowUpReminderSentAt { get; private set; }

    public static UserNotificationPreference Create(Guid userId, DateTimeOffset createdAt)
    {
        DomainGuards.NotEmpty(userId, nameof(userId));
        return new UserNotificationPreference(userId, createdAt);
    }

    public bool Update(
        bool sharingActivityEmailEnabled,
        bool dailySummaryEmailEnabled,
        bool followUpReminderEmailEnabled,
        DateTimeOffset updatedAt)
    {
        if (SharingActivityEmailEnabled == sharingActivityEmailEnabled &&
            DailySummaryEmailEnabled == dailySummaryEmailEnabled &&
            FollowUpReminderEmailEnabled == followUpReminderEmailEnabled)
        {
            return false;
        }

        SharingActivityEmailEnabled = sharingActivityEmailEnabled;
        DailySummaryEmailEnabled = dailySummaryEmailEnabled;
        FollowUpReminderEmailEnabled = followUpReminderEmailEnabled;
        UpdatedAt = updatedAt;
        return true;
    }
}
