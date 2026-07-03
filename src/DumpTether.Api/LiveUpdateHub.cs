using System.Security.Claims;
using DumpTether.App.Auth;
using DumpTether.App.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DumpTether.Api;

[Authorize(Policy = AuthPolicies.SessionRequired)]
internal sealed class LiveUpdateHub : Hub
{
    private readonly IAuthRepository _authRepository;
    private readonly IClock _clock;

    public LiveUpdateHub(IAuthRepository authRepository, IClock clock)
    {
        _authRepository = authRepository;
        _clock = clock;
    }

    public override async Task OnConnectedAsync()
    {
        var currentSession = await GetCurrentSessionAsync();

        if (currentSession is null)
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            UserGroup(currentSession.UserId),
            Context.ConnectionAborted);

        var workspaces = await _authRepository.ListWorkspacesForUserAsync(
            currentSession.UserId,
            Context.ConnectionAborted);

        foreach (var workspace in workspaces.Where(workspace =>
                     workspace.AccessKind == WorkspaceAccessKinds.Membership))
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                WorkspaceGroup(workspace.Workspace.Id),
                Context.ConnectionAborted);
        }

        await base.OnConnectedAsync();
    }

    public async Task JoinWorkspace(Guid workspaceId)
    {
        if (workspaceId == Guid.Empty)
        {
            return;
        }

        var currentSession = await GetCurrentSessionAsync();

        if (currentSession is null)
        {
            Context.Abort();
            return;
        }

        var workspaces = await _authRepository.ListWorkspacesForUserAsync(
            currentSession.UserId,
            Context.ConnectionAborted);

        if (!workspaces.Any(workspace =>
                workspace.AccessKind == WorkspaceAccessKinds.Membership &&
                workspace.Workspace.Id == workspaceId))
        {
            return;
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            WorkspaceGroup(workspaceId),
            Context.ConnectionAborted);
    }

    private async Task<CurrentUserSession?> GetCurrentSessionAsync()
    {
        var user = Context.User;
        var rawUserId = user?.FindFirstValue(ClaimTypes.NameIdentifier);
        var rawSessionId = user?.FindFirstValue("dumptether:session_id");
        var email = user?.FindFirstValue(ClaimTypes.Email);
        var displayName = user?.FindFirstValue(ClaimTypes.Name);

        if (!Guid.TryParse(rawUserId, out var userId) ||
            !Guid.TryParse(rawSessionId, out var sessionId) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        var session = await _authRepository.GetSessionByIdAsync(
            sessionId,
            trackChanges: false,
            Context.ConnectionAborted);

        if (session is null ||
            session.UserId != userId ||
            !session.IsUsable(_clock.UtcNow))
        {
            Context.Abort();
            return null;
        }

        return new CurrentUserSession(
            userId,
            sessionId,
            email,
            displayName,
            session.SessionType,
            session.DeviceName,
            session.CreatedAt,
            session.ExpiresAt,
            session.LastSeenAt);
    }

    public static string WorkspaceGroup(Guid workspaceId)
    {
        return $"workspace:{workspaceId:D}";
    }

    public static string UserGroup(Guid userId)
    {
        return $"user:{userId:D}";
    }
}
