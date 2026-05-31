using System.ComponentModel.DataAnnotations;
using DumpTether.App.Tasks;
using DumpTether.App.Workspaces;
using DumpTether.Domain;

namespace DumpTether.App.Auth;

internal sealed class AuthService : IAuthService
{
    private static readonly TimeSpan SessionDuration = TimeSpan.FromDays(30);

    private readonly IAuthRepository _authRepository;
    private readonly IAuthTokenAccessor _authTokenAccessor;
    private readonly IClock _clock;
    private readonly ICurrentUserSessionProvider _currentUserSessionProvider;
    private readonly IPasswordHashService _passwordHashService;
    private readonly ISessionTokenService _sessionTokenService;
    private readonly IWorkspaceRepository _workspaceRepository;

    public AuthService(
        IAuthRepository authRepository,
        IAuthTokenAccessor authTokenAccessor,
        IClock clock,
        ICurrentUserSessionProvider currentUserSessionProvider,
        IPasswordHashService passwordHashService,
        ISessionTokenService sessionTokenService,
        IWorkspaceRepository workspaceRepository)
    {
        _authRepository = authRepository;
        _authTokenAccessor = authTokenAccessor;
        _clock = clock;
        _currentUserSessionProvider = currentUserSessionProvider;
        _passwordHashService = passwordHashService;
        _sessionTokenService = sessionTokenService;
        _workspaceRepository = workspaceRepository;
    }

    public async Task<RegisterUserResponse> RegisterAsync(
        RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedEmail = AppUser.NormalizeEmail(request.Email);
        var existingUser = await _authRepository.GetUserByNormalizedEmailAsync(
            normalizedEmail,
            trackChanges: false,
            cancellationToken);

        if (existingUser is not null)
        {
            throw new ValidationException("Email is already registered.");
        }

        var now = _clock.UtcNow;
        var user = AppUser.Create(
            request.Email,
            request.DisplayName,
            _passwordHashService.HashPassword(request.Password),
            now);
        var workspace = Workspace.Create("All Tasks", now);
        var membership = WorkspaceMembership.Create(
            workspace.Id,
            user.Id,
            WorkspaceMembershipRole.Owner,
            now);

        await _authRepository.AddUserAsync(user, cancellationToken);
        await _workspaceRepository.AddAsync(workspace, cancellationToken);
        await _authRepository.AddWorkspaceMembershipAsync(membership, cancellationToken);
        await _authRepository.SaveChangesAsync(cancellationToken);

        return new RegisterUserResponse(
            MapUser(user),
            MapWorkspace(workspace, membership));
    }

    public async Task<LoginUserResponse> LoginAsync(
        LoginUserRequest request,
        AuthRequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await _authRepository.GetUserByNormalizedEmailAsync(
            AppUser.NormalizeEmail(request.Email),
            trackChanges: true,
            cancellationToken);

        if (user is null ||
            !user.IsActive ||
            !_passwordHashService.VerifyPassword(user.PasswordHash, request.Password))
        {
            throw new ValidationException("Invalid email or password.");
        }

        var now = _clock.UtcNow;
        var sessionToken = _sessionTokenService.CreateSessionToken();
        var session = UserSession.Create(
            user.Id,
            _sessionTokenService.HashToken(sessionToken),
            now,
            now.Add(SessionDuration),
            metadata.UserAgent,
            _sessionTokenService.HashOptionalMetadata(metadata.IpAddress),
            request.DeviceName);

        user.MarkLoggedIn(now);
        await _authRepository.AddSessionAsync(session, cancellationToken);
        await _authRepository.SaveChangesAsync(cancellationToken);

        var workspaces = await _authRepository.ListWorkspacesForUserAsync(user.Id, cancellationToken);

        return new LoginUserResponse(
            MapUser(user),
            workspaces.Select(MapWorkspace).ToList(),
            sessionToken,
            session.ExpiresAt);
    }

    public async Task<bool> LogoutAsync(CancellationToken cancellationToken)
    {
        var token = _authTokenAccessor.SessionToken;

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var session = await _authRepository.GetSessionByTokenHashAsync(
            _sessionTokenService.HashToken(token),
            trackChanges: true,
            cancellationToken);

        if (session is null || !session.IsUsable(_clock.UtcNow))
        {
            return false;
        }

        session.Revoke(_clock.UtcNow);
        await _authRepository.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<CurrentUserResponse?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var current = await _currentUserSessionProvider.GetCurrentAsync(cancellationToken);

        if (current is null)
        {
            return null;
        }

        var user = await _authRepository.GetUserByIdAsync(
            current.UserId,
            trackChanges: false,
            cancellationToken);

        if (user is null || !user.IsActive)
        {
            return null;
        }

        var workspaces = await _authRepository.ListWorkspacesForUserAsync(
            user.Id,
            cancellationToken);

        return new CurrentUserResponse(
            MapUser(user),
            workspaces.Select(MapWorkspace).ToList());
    }

    private static AuthUserResponse MapUser(AppUser user)
    {
        return new AuthUserResponse(
            user.Id,
            user.Email,
            user.DisplayName,
            user.CreatedAt,
            user.LastLoginAt);
    }

    private static AuthWorkspaceResponse MapWorkspace(UserWorkspaceMembership membership)
    {
        return MapWorkspace(membership.Workspace, membership.Membership);
    }

    private static AuthWorkspaceResponse MapWorkspace(
        Workspace workspace,
        WorkspaceMembership membership)
    {
        return new AuthWorkspaceResponse(
            workspace.Id,
            workspace.Name,
            workspace.Color,
            membership.Role);
    }
}
