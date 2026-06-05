namespace DumpTether.App.LiveUpdates;

public sealed record LiveUpdateMessage(
    string EventName,
    Guid WorkspaceId,
    Guid? TaskItemId,
    Guid? TimelineEntryId,
    Guid? ActorUserId,
    DateTimeOffset OccurredAt,
    DateTimeOffset? UpdatedAt = null,
    IReadOnlyList<Guid>? RecipientUserIds = null);
