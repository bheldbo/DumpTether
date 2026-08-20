using DumpTether.App.Notifications;
using Microsoft.Extensions.Options;

namespace DumpTether.Api;

internal sealed class NotificationDeliveryHostedService : BackgroundService
{
    private readonly ILogger<NotificationDeliveryHostedService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public NotificationDeliveryHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationDeliveryHostedService> logger)
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
                    "Scheduled notification processing failed; it will retry on the next interval.");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    internal async Task ProcessOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<IUserNotificationService>()
            .ProcessScheduledAsync(cancellationToken);
    }

    private TimeSpan GetInterval()
    {
        using var scope = _scopeFactory.CreateScope();
        var minutes = scope.ServiceProvider
            .GetRequiredService<IOptions<NotificationOptions>>()
            .Value
            .SweepIntervalMinutes;
        return TimeSpan.FromMinutes(Math.Clamp(minutes, 1, 1440));
    }
}
