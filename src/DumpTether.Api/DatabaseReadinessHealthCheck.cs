using DumpTether.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DumpTether.Api;

internal sealed class DatabaseReadinessHealthCheck(
    IServiceScopeFactory scopeFactory) : IHealthCheck
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(5);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private HealthCheckResult? _cachedResult;
    private DateTimeOffset _checkedAt;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var cached = ReadFreshResult();
        if (cached is not null)
        {
            return cached.Value;
        }

        await _gate.WaitAsync(cancellationToken);

        try
        {
            cached = ReadFreshResult();
            if (cached is not null)
            {
                return cached.Value;
            }

            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
            var result = await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("Database connection is ready.")
                : HealthCheckResult.Unhealthy("Database connection is unavailable.");

            _cachedResult = result;
            _checkedAt = DateTimeOffset.UtcNow;
            return result;
        }
        catch
        {
            var result = HealthCheckResult.Unhealthy("Database connection is unavailable.");
            _cachedResult = result;
            _checkedAt = DateTimeOffset.UtcNow;
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    private HealthCheckResult? ReadFreshResult() =>
        _cachedResult is not null &&
        DateTimeOffset.UtcNow - _checkedAt < CacheDuration
            ? _cachedResult
            : null;
}
