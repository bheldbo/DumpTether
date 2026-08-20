namespace DumpTether.App.Notifications;

public sealed record AccountNotificationPreferencesResponse(
    bool EmailDeliveryAvailable,
    bool SharingActivityEmailEnabled,
    bool DailySummaryEmailEnabled,
    bool FollowUpReminderEmailEnabled);

public sealed record UpdateAccountNotificationPreferencesRequest(
    bool SharingActivityEmailEnabled,
    bool DailySummaryEmailEnabled,
    bool FollowUpReminderEmailEnabled);

public sealed record NotificationTaskDigestItem(
    string Title,
    string WorkspaceName,
    DateTimeOffset? FollowUpAt);

public sealed record NotificationDigestSnapshot(
    string Email,
    string DisplayName,
    int ActiveTaskCount,
    int UpdatedTaskCount,
    int OverdueFollowUpCount,
    IReadOnlyList<NotificationTaskDigestItem> FollowUps);

public enum NotificationDigestKind
{
    DailySummary = 0,
    FollowUpReminder = 1
}
