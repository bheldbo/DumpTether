using DumpTether.App.LiveUpdates;
using Microsoft.AspNetCore.SignalR;

namespace DumpTether.Api;

internal sealed class SignalRLiveUpdatePublisher : ILiveUpdatePublisher
{
    private readonly IHubContext<LiveUpdateHub> _hubContext;

    public SignalRLiveUpdatePublisher(IHubContext<LiveUpdateHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task PublishAsync(
        LiveUpdateMessage message,
        CancellationToken cancellationToken)
    {
        var publishTasks = new List<Task>
        {
            _hubContext.Clients
                .Group(LiveUpdateHub.WorkspaceGroup(message.WorkspaceId))
                .SendAsync("LiveUpdate", message, cancellationToken)
        };

        foreach (var userId in (message.RecipientUserIds ?? [])
                     .Where(userId => userId != Guid.Empty)
                     .Distinct())
        {
            publishTasks.Add(
                _hubContext.Clients
                    .Group(LiveUpdateHub.UserGroup(userId))
                    .SendAsync("LiveUpdate", message, cancellationToken));
        }

        await Task.WhenAll(publishTasks);
    }
}
