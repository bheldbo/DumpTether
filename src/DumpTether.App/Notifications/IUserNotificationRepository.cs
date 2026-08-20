using DumpTether.Domain;

namespace DumpTether.App.Notifications;

public interface IUserNotificationRepository
{
    Task<UserNotificationPreference?> GetAsync(
        Guid userId,
        bool trackChanges,
        CancellationToken cancellationToken);

    Task AddAsync(
        UserNotificationPreference preference,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<UserNotificationPreference>> ListEnabledAsync(
        CancellationToken cancellationToken);

    Task<bool> TryClaimAsync(
        Guid userId,
        NotificationDigestKind kind,
        DateTimeOffset scheduledFor,
        DateTimeOffset claimedAt,
        DateTimeOffset staleClaimBefore,
        CancellationToken cancellationToken);

    Task MarkSentAsync(
        Guid userId,
        NotificationDigestKind kind,
        DateTimeOffset claimedAt,
        DateTimeOffset sentAt,
        CancellationToken cancellationToken);

    Task ReleaseClaimAsync(
        Guid userId,
        NotificationDigestKind kind,
        DateTimeOffset claimedAt,
        CancellationToken cancellationToken);

    Task<NotificationDigestSnapshot?> GetDigestSnapshotAsync(
        Guid userId,
        DateTimeOffset updatedSince,
        DateTimeOffset now,
        DateTimeOffset followUpThrough,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
