namespace DumpTether.App.Notifications;

public interface IUserNotificationService
{
    Task<AccountNotificationPreferencesResponse> GetCurrentAsync(
        CancellationToken cancellationToken);

    Task<AccountNotificationPreferencesResponse> UpdateCurrentAsync(
        UpdateAccountNotificationPreferencesRequest request,
        CancellationToken cancellationToken);

    Task NotifySharingAcceptedAsync(
        Guid ownerUserId,
        string acceptedByDisplayName,
        string resourceName,
        int resourceCount,
        CancellationToken cancellationToken);

    Task ProcessScheduledAsync(CancellationToken cancellationToken);
}
