using DumpTether.App.Auth;
using DumpTether.App.Email;
using DumpTether.App.Tasks;
using DumpTether.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DumpTether.App.Notifications;

internal sealed class UserNotificationService : IUserNotificationService
{
    private static readonly TimeSpan StaleClaimAge = TimeSpan.FromMinutes(30);
    private readonly IAuthRepository _authRepository;
    private readonly IClock _clock;
    private readonly ICurrentUserSessionProvider _currentUserSessionProvider;
    private readonly IEmailSender _emailSender;
    private readonly IOptions<EmailOptions> _emailOptions;
    private readonly ILogger<UserNotificationService> _logger;
    private readonly IOptions<NotificationOptions> _notificationOptions;
    private readonly IUserNotificationRepository _repository;

    public UserNotificationService(
        IAuthRepository authRepository,
        IClock clock,
        ICurrentUserSessionProvider currentUserSessionProvider,
        IEmailSender emailSender,
        IOptions<EmailOptions> emailOptions,
        ILogger<UserNotificationService> logger,
        IOptions<NotificationOptions> notificationOptions,
        IUserNotificationRepository repository)
    {
        _authRepository = authRepository;
        _clock = clock;
        _currentUserSessionProvider = currentUserSessionProvider;
        _emailSender = emailSender;
        _emailOptions = emailOptions;
        _logger = logger;
        _notificationOptions = notificationOptions;
        _repository = repository;
    }

    public async Task<AccountNotificationPreferencesResponse> GetCurrentAsync(
        CancellationToken cancellationToken)
    {
        var session = await RequireAccountSessionAsync(cancellationToken);
        var preference = await _repository.GetAsync(
            session.UserId,
            trackChanges: false,
            cancellationToken);
        return Map(preference);
    }

    public async Task<AccountNotificationPreferencesResponse> UpdateCurrentAsync(
        UpdateAccountNotificationPreferencesRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var session = await RequireAccountSessionAsync(cancellationToken);
        var preference = await _repository.GetAsync(
            session.UserId,
            trackChanges: true,
            cancellationToken);
        if (preference is null)
        {
            preference = UserNotificationPreference.Create(session.UserId, _clock.UtcNow);
            await _repository.AddAsync(preference, cancellationToken);
        }

        preference.Update(
            request.SharingActivityEmailEnabled,
            request.DailySummaryEmailEnabled,
            request.FollowUpReminderEmailEnabled,
            _clock.UtcNow);
        await _repository.SaveChangesAsync(cancellationToken);
        return Map(preference);
    }

    public async Task NotifySharingAcceptedAsync(
        Guid ownerUserId,
        string acceptedByDisplayName,
        string resourceName,
        int resourceCount,
        CancellationToken cancellationToken)
    {
        if (!EmailDeliveryAvailable)
        {
            return;
        }

        try
        {
            var preference = await _repository.GetAsync(
                ownerUserId,
                trackChanges: false,
                cancellationToken);
            if (preference?.SharingActivityEmailEnabled != true)
            {
                return;
            }

            var owner = await _authRepository.GetUserByIdAsync(
                ownerUserId,
                trackChanges: false,
                cancellationToken);
            if (owner is null || !owner.IsActive)
            {
                return;
            }

            await _emailSender.SendAsync(
                NotificationEmailBuilders.SharingAccepted(
                    owner.Email,
                    owner.DisplayName,
                    acceptedByDisplayName,
                    resourceName,
                    Math.Max(1, resourceCount)),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Sharing acceptance notification could not be completed. OwnerUserId: {OwnerUserId}.",
                ownerUserId);
        }
    }

    public async Task ProcessScheduledAsync(CancellationToken cancellationToken)
    {
        if (!EmailDeliveryAvailable)
        {
            return;
        }

        var options = _notificationOptions.Value;
        var now = _clock.UtcNow;
        var scheduledFor = GetMostRecentSchedule(now, options.DailyDigestHourUtc);
        if (!scheduledFor.HasValue)
        {
            return;
        }

        var staleClaimBefore = now.Subtract(StaleClaimAge);
        var preferences = await _repository.ListEnabledAsync(cancellationToken);
        foreach (var preference in preferences)
        {
            if (preference.DailySummaryEmailEnabled)
            {
                await ProcessDigestAsync(
                    preference.UserId,
                    NotificationDigestKind.DailySummary,
                    scheduledFor.Value,
                    now,
                    staleClaimBefore,
                    cancellationToken);
            }

            if (preference.FollowUpReminderEmailEnabled)
            {
                await ProcessDigestAsync(
                    preference.UserId,
                    NotificationDigestKind.FollowUpReminder,
                    scheduledFor.Value,
                    now,
                    staleClaimBefore,
                    cancellationToken);
            }
        }
    }

    private bool EmailDeliveryAvailable =>
        _notificationOptions.Value.Enabled &&
        _emailOptions.Value.Provider != EmailProvider.None;

    private async Task ProcessDigestAsync(
        Guid userId,
        NotificationDigestKind kind,
        DateTimeOffset scheduledFor,
        DateTimeOffset now,
        DateTimeOffset staleClaimBefore,
        CancellationToken cancellationToken)
    {
        if (!await _repository.TryClaimAsync(
                userId,
                kind,
                scheduledFor,
                now,
                staleClaimBefore,
                cancellationToken))
        {
            return;
        }

        try
        {
            var snapshot = await _repository.GetDigestSnapshotAsync(
                userId,
                now.AddDays(-1),
                now,
                now.AddHours(Math.Clamp(_notificationOptions.Value.FollowUpWindowHours, 1, 168)),
                cancellationToken);
            if (snapshot is null ||
                (kind == NotificationDigestKind.DailySummary && snapshot.ActiveTaskCount == 0) ||
                (kind == NotificationDigestKind.FollowUpReminder && snapshot.FollowUps.Count == 0))
            {
                await _repository.MarkSentAsync(userId, kind, now, now, cancellationToken);
                return;
            }

            var message = kind == NotificationDigestKind.DailySummary
                ? NotificationEmailBuilders.DailySummary(snapshot)
                : NotificationEmailBuilders.FollowUpReminder(snapshot);
            await _emailSender.SendAsync(message, cancellationToken);
            await _repository.MarkSentAsync(userId, kind, now, now, cancellationToken);
        }
        catch (Exception exception) when (exception is EmailDeliveryException or HttpRequestException)
        {
            await _repository.ReleaseClaimAsync(userId, kind, now, cancellationToken);
            _logger.LogWarning(
                exception,
                "Scheduled notification delivery failed. UserId: {UserId}; Kind: {Kind}.",
                userId,
                kind);
        }
    }

    private AccountNotificationPreferencesResponse Map(UserNotificationPreference? preference) =>
        new(
            EmailDeliveryAvailable,
            preference?.SharingActivityEmailEnabled ?? false,
            preference?.DailySummaryEmailEnabled ?? false,
            preference?.FollowUpReminderEmailEnabled ?? false);

    private async Task<CurrentUserSession> RequireAccountSessionAsync(
        CancellationToken cancellationToken)
    {
        var session = await _currentUserSessionProvider.GetCurrentAsync(cancellationToken) ??
            throw new UnauthorizedAccessException("Authentication is required.");
        if (session.SessionType is UserSessionType.DesktopLocal or UserSessionType.Guest)
        {
            throw new UnauthorizedAccessException("A cloud account is required for email notifications.");
        }

        return session;
    }

    private static DateTimeOffset? GetMostRecentSchedule(DateTimeOffset now, int hourUtc)
    {
        var hour = Math.Clamp(hourUtc, 0, 23);
        var scheduled = new DateTimeOffset(now.UtcDateTime.Date.AddHours(hour), TimeSpan.Zero);
        return now >= scheduled ? scheduled : null;
    }
}
