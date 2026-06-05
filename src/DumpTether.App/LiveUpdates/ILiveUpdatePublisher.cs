namespace DumpTether.App.LiveUpdates;

public interface ILiveUpdatePublisher
{
    Task PublishAsync(
        LiveUpdateMessage message,
        CancellationToken cancellationToken);
}
