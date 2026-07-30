using DumpTether.App.LiveUpdates;
using DumpTether.App.Sync;
using DumpTether.App.Tasks;
using DumpTether.Domain;
using Microsoft.AspNetCore.SignalR.Client;

namespace DumpTether.Api;

internal sealed class DesktopCloudLiveUpdateRelayHostedService : BackgroundService
{
    private static readonly TimeSpan AccountRefreshInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan[] ReconnectDelays =
    [
        TimeSpan.Zero,
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30)
    ];

    private readonly ICloudSessionProtector _cloudSessionProtector;
    private readonly IClock _clock;
    private readonly ILogger<DesktopCloudLiveUpdateRelayHostedService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Dictionary<Guid, RelayConnection> _connections = [];

    public DesktopCloudLiveUpdateRelayHostedService(
        ICloudSessionProtector cloudSessionProtector,
        IClock clock,
        ILogger<DesktopCloudLiveUpdateRelayHostedService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _cloudSessionProtector = cloudSessionProtector;
        _clock = clock;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ReconcileConnectionsAsync(stoppingToken);
                await Task.Delay(AccountRefreshInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal hosted-service shutdown.
        }
        finally
        {
            await DisposeConnectionsAsync();
        }
    }

    private async Task ReconcileConnectionsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<CloudSyncAccount> accounts;
        using (var scope = _scopeFactory.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<ISyncRepository>();
            accounts = await repository.ListConnectedCloudAccountsAsync(
                _clock.UtcNow,
                cancellationToken);
        }

        var descriptors = accounts
            .Select(account => new RelayAccount(
                account.Id,
                account.UserId,
                account.CloudUserId,
                account.CloudApiBaseUrl,
                account.ProtectedSessionToken,
                account.SessionExpiresAt))
            .ToDictionary(account => account.Id);

        foreach (var staleAccountId in _connections.Keys
                     .Where(accountId => !descriptors.ContainsKey(accountId))
                     .ToArray())
        {
            await RemoveConnectionAsync(staleAccountId);
        }

        foreach (var descriptor in descriptors.Values)
        {
            if (_connections.TryGetValue(descriptor.Id, out var existing) &&
                existing.Account == descriptor &&
                existing.Connection.State != HubConnectionState.Disconnected)
            {
                continue;
            }

            await RemoveConnectionAsync(descriptor.Id);
            await TryStartConnectionAsync(descriptor, cancellationToken);
        }
    }

    private async Task TryStartConnectionAsync(
        RelayAccount account,
        CancellationToken cancellationToken)
    {
        HubConnection? connection = null;
        try
        {
            var sessionToken = _cloudSessionProtector.Unprotect(account.ProtectedSessionToken);
            connection = new HubConnectionBuilder()
                .WithUrl(
                    $"{account.CloudApiBaseUrl.TrimEnd('/')}/api/live",
                    options =>
                    {
                        options.AccessTokenProvider = () =>
                            Task.FromResult<string?>(sessionToken);
                    })
                .WithAutomaticReconnect(ReconnectDelays)
                .Build();

            connection.On<LiveUpdateMessage>(
                "LiveUpdate",
                message => RelayCloudUpdateAsync(account, message));
            connection.Reconnected += _ => RelayCatalogAttentionAsync(account);
            connection.Closed += error =>
            {
                if (error is not null)
                {
                    _logger.LogWarning(
                        "Desktop cloud live-update connection closed for account {CloudAccountId}.",
                        account.Id);
                }

                return Task.CompletedTask;
            };

            await connection.StartAsync(cancellationToken);
            _connections[account.Id] = new RelayConnection(account, connection);
            connection = null;
            await RelayCatalogAttentionAsync(account);
            _logger.LogInformation(
                "Desktop cloud live-update relay connected for account {CloudAccountId}.",
                account.Id);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Desktop cloud live-update relay could not connect for account {CloudAccountId}; reconciliation will retry.",
                account.Id);
        }
        finally
        {
            if (connection is not null)
            {
                await connection.DisposeAsync();
            }
        }
    }

    private async Task RelayCloudUpdateAsync(
        RelayAccount account,
        LiveUpdateMessage cloudMessage)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ISyncRepository>();
            var publisher = scope.ServiceProvider.GetRequiredService<ILiveUpdatePublisher>();
            var root = cloudMessage.WorkspaceId == Guid.Empty
                ? null
                : await repository.GetRootByRemoteWorkspaceAsync(
                    cloudMessage.WorkspaceId,
                    account.CloudUserId,
                    trackChanges: false,
                    CancellationToken.None);
            var catalogChanged = root is null ||
                cloudMessage.EventName is
                    LiveUpdateEvents.WorkspaceCreated or
                    LiveUpdateEvents.WorkspaceDeleted or
                    LiveUpdateEvents.WorkspaceInviteAccepted or
                    LiveUpdateEvents.WorkspaceAccessChanged;

            await publisher.PublishAsync(
                new LiveUpdateMessage(
                    catalogChanged
                        ? LiveUpdateEvents.CloudCatalogChanged
                        : LiveUpdateEvents.CloudChangeAvailable,
                    root?.LocalWorkspaceId ?? Guid.Empty,
                    TaskItemId: null,
                    TimelineEntryId: null,
                    ActorUserId: null,
                    cloudMessage.OccurredAt,
                    cloudMessage.UpdatedAt,
                    [account.LocalUserId]),
                CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Desktop cloud update could not be relayed for account {CloudAccountId}; periodic reconciliation remains active.",
                account.Id);
        }
    }

    private Task RelayCatalogAttentionAsync(RelayAccount account)
    {
        return RelayCloudUpdateAsync(
            account,
            new LiveUpdateMessage(
                LiveUpdateEvents.CloudCatalogChanged,
                Guid.Empty,
                TaskItemId: null,
                TimelineEntryId: null,
                ActorUserId: null,
                _clock.UtcNow,
                UpdatedAt: null));
    }

    private async Task RemoveConnectionAsync(Guid accountId)
    {
        if (!_connections.Remove(accountId, out var relay))
        {
            return;
        }

        try
        {
            await relay.Connection.StopAsync();
        }
        catch
        {
            // Disposal is still required if the remote endpoint disappeared.
        }

        await relay.Connection.DisposeAsync();
    }

    private async Task DisposeConnectionsAsync()
    {
        foreach (var accountId in _connections.Keys.ToArray())
        {
            await RemoveConnectionAsync(accountId);
        }
    }

    private sealed record RelayAccount(
        Guid Id,
        Guid LocalUserId,
        Guid CloudUserId,
        string CloudApiBaseUrl,
        string ProtectedSessionToken,
        DateTimeOffset SessionExpiresAt);

    private sealed record RelayConnection(
        RelayAccount Account,
        HubConnection Connection);
}
