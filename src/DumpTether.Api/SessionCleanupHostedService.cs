using DumpTether.App.Auth;
using DumpTether.App.Tasks;
using Microsoft.Extensions.Options;

namespace DumpTether.Api;

internal sealed class SessionCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SessionCleanupHostedService> _logger;

    public SessionCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<SessionCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var interval = GetInterval();

            if (interval is null)
            {
                _logger.LogInformation("Session cleanup hosted service is disabled.");
                return;
            }

            try
            {
                await Task.Delay(interval.Value, stoppingToken);
                await CleanupSessionsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Session cleanup hosted service failed. It will retry on the next interval.");
            }
        }
    }

    private TimeSpan? GetInterval()
    {
        using var scope = _scopeFactory.CreateScope();
        var options = scope.ServiceProvider
            .GetRequiredService<IOptions<AuthOptions>>()
            .Value;

        if (options.SessionCleanupDays <= 0 ||
            options.SessionCleanupIntervalHours <= 0)
        {
            return null;
        }

        return TimeSpan.FromHours(Math.Clamp(options.SessionCleanupIntervalHours, 1, 168));
    }

    private async Task CleanupSessionsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var options = scope.ServiceProvider
            .GetRequiredService<IOptions<AuthOptions>>()
            .Value;
        var authRepository = scope.ServiceProvider.GetRequiredService<IAuthRepository>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var now = clock.UtcNow;
        var cleanupDays = Math.Clamp(options.SessionCleanupDays, 1, 3650);
        var deleteBefore = now.AddDays(-cleanupDays);
        var deletedSessions = await authRepository.DeleteInactiveSessionsAsync(
            now,
            deleteBefore,
            cancellationToken);
        var deletedTokens = await authRepository.DeleteInactiveAuthTokensAsync(
            now,
            deleteBefore,
            cancellationToken);

        if (deletedSessions > 0 || deletedTokens > 0)
        {
            await authRepository.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Deleted {SessionCount} inactive user sessions and {TokenCount} inactive auth tokens.",
                deletedSessions,
                deletedTokens);
        }
    }
}
