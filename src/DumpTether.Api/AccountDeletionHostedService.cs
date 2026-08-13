using DumpTether.App.Administration;
using DumpTether.App.Auth;
using DumpTether.App.Email;
using DumpTether.App.Tasks;
using Microsoft.Extensions.Options;

namespace DumpTether.Api;

internal sealed class AccountDeletionHostedService : BackgroundService
{
    private static readonly TimeSpan StaleClaimAge = TimeSpan.FromMinutes(30);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AccountDeletionHostedService> _logger;

    public AccountDeletionHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<AccountDeletionHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOnceAsync(stoppingToken);
                await Task.Delay(GetInterval(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Account deletion lifecycle processing failed; it will retry on the next interval.");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private TimeSpan GetInterval()
    {
        using var scope = _scopeFactory.CreateScope();
        var minutes = scope.ServiceProvider
            .GetRequiredService<IOptions<AccountDeletionOptions>>()
            .Value
            .SweepIntervalMinutes;
        return TimeSpan.FromMinutes(Math.Clamp(minutes, 1, 1440));
    }

    internal async Task ProcessOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<AccountDeletionOptions>>().Value;
        if (!options.Enabled)
        {
            return;
        }

        var repository = scope.ServiceProvider.GetRequiredService<IAuthRepository>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var now = clock.UtcNow;
        var staleClaimBefore = now.Subtract(StaleClaimAge);
        await SendRemindersAsync(scope.ServiceProvider, repository, now, staleClaimBefore, cancellationToken);
        await DeleteDueAccountsAsync(scope.ServiceProvider, repository, now, staleClaimBefore, cancellationToken);
    }

    private async Task SendRemindersAsync(
        IServiceProvider services,
        IAuthRepository repository,
        DateTimeOffset now,
        DateTimeOffset staleClaimBefore,
        CancellationToken cancellationToken)
    {
        var sender = services.GetRequiredService<IEmailSender>();
        var requests = await repository.ListAccountDeletionRemindersDueAsync(
            now,
            staleClaimBefore,
            cancellationToken);
        foreach (var request in requests)
        {
            if (!await repository.TryClaimAccountDeletionReminderAsync(
                    request.Id,
                    now,
                    staleClaimBefore,
                    cancellationToken))
            {
                continue;
            }

            try
            {
                var user = await repository.GetUserByIdAsync(
                    request.UserId,
                    trackChanges: false,
                    cancellationToken);
                if (user is null)
                {
                    await repository.ReleaseAccountDeletionReminderClaimAsync(
                        request.Id,
                        cancellationToken);
                    continue;
                }

                await sender.SendAsync(
                    AccountEmailBuilders.AccountDeletionReminder(
                        user.Email,
                        user.DisplayName,
                        request.ScheduledFor),
                    cancellationToken);
                await repository.MarkAccountDeletionReminderSentAsync(
                    request.Id,
                    now,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is EmailDeliveryException or HttpRequestException)
            {
                await repository.ReleaseAccountDeletionReminderClaimAsync(request.Id, cancellationToken);
                _logger.LogWarning(
                    exception,
                    "Account deletion reminder delivery failed. RequestId: {RequestId}.",
                    request.Id);
            }
        }
    }

    private async Task DeleteDueAccountsAsync(
        IServiceProvider services,
        IAuthRepository repository,
        DateTimeOffset now,
        DateTimeOffset staleClaimBefore,
        CancellationToken cancellationToken)
    {
        var administration = services.GetRequiredService<IAdministrationService>();
        var requests = await repository.ListAccountDeletionsDueAsync(
            now,
            staleClaimBefore,
            cancellationToken);
        foreach (var request in requests)
        {
            if (!await repository.TryClaimAccountDeletionAsync(
                    request.Id,
                    now,
                    staleClaimBefore,
                    cancellationToken))
            {
                continue;
            }

            try
            {
                var user = await repository.GetUserByIdAsync(
                    request.UserId,
                    trackChanges: false,
                    cancellationToken);
                if (user is null)
                {
                    await repository.ReleaseAccountDeletionClaimAsync(request.Id, cancellationToken);
                    continue;
                }

                if (await repository.HasOwnedWorkspaceSharedWithOthersAsync(user.Id, cancellationToken))
                {
                    await repository.ReleaseAccountDeletionClaimAsync(request.Id, cancellationToken);
                    _logger.LogWarning(
                        "Scheduled account deletion was paused because owned boards are shared. UserId: {UserId}.",
                        user.Id);
                    continue;
                }

                await administration.DeleteUserAsync(
                    user.Email,
                    user.Email,
                    "account-deletion-worker",
                    "User-requested deletion grace period elapsed.",
                    cancellationToken);
                _logger.LogInformation("Completed scheduled account deletion. UserId: {UserId}.", user.Id);
            }
            catch (Exception exception)
            {
                await repository.ReleaseAccountDeletionClaimAsync(request.Id, cancellationToken);
                _logger.LogError(
                    exception,
                    "Scheduled account deletion failed. RequestId: {RequestId}.",
                    request.Id);
            }
        }
    }
}
