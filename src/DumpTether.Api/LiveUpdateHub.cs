using DumpTether.App.Auth;
using DumpTether.App.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Net.Http.Headers;

namespace DumpTether.Api;

internal sealed class LiveUpdateHub : Hub
{
    private const string SessionCookieName = "DumpTether.Session";
    private readonly IAuthRepository _authRepository;
    private readonly IClock _clock;
    private readonly ISessionTokenService _sessionTokenService;

    public LiveUpdateHub(
        IAuthRepository authRepository,
        IClock clock,
        ISessionTokenService sessionTokenService)
    {
        _authRepository = authRepository;
        _clock = clock;
        _sessionTokenService = sessionTokenService;
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

        foreach (var workspace in workspaces)
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

        if (!workspaces.Any(workspace => workspace.Workspace.Id == workspaceId))
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
        var token = GetSessionToken();

        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var session = await _authRepository.GetSessionByTokenHashAsync(
            _sessionTokenService.HashToken(token),
            trackChanges: false,
            Context.ConnectionAborted);

        if (session is null || !session.IsUsable(_clock.UtcNow))
        {
            return null;
        }

        var user = await _authRepository.GetUserByIdAsync(
            session.UserId,
            trackChanges: false,
            Context.ConnectionAborted);

        if (user is null || !user.IsActive)
        {
            return null;
        }

        return new CurrentUserSession(
            user.Id,
            session.Id,
            user.Email,
            user.DisplayName);
    }

    private string? GetSessionToken()
    {
        var httpContext = Context.GetHttpContext();
        var authorization = httpContext?.Request.Headers[HeaderNames.Authorization].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(authorization) &&
            authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authorization["Bearer ".Length..].Trim();
        }

        var queryToken = httpContext?.Request.Query["access_token"].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(queryToken))
        {
            return queryToken;
        }

        return httpContext?.Request.Cookies[SessionCookieName];
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
