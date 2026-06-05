namespace DumpTether.App.LiveUpdates;

internal sealed class NoOpLiveUpdatePublisher : ILiveUpdatePublisher
{
    public Task PublishAsync(
        LiveUpdateMessage message,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
