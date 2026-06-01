using System.ComponentModel.DataAnnotations;
using System.Net;
using DumpTether.App.Email;
using DumpTether.App.Tasks;
using DumpTether.App.Workspaces;
using DumpTether.Domain;
using Microsoft.Extensions.Options;

namespace DumpTether.App.Auth;

internal sealed class AuthService : IAuthService
{
    private static readonly TimeSpan SessionDuration = TimeSpan.FromDays(30);

    private readonly IAuthRepository _authRepository;
    private readonly IAuthTokenAccessor _authTokenAccessor;
    private readonly IClock _clock;
    private readonly ICurrentUserSessionProvider _currentUserSessionProvider;
    private readonly IOptions<AuthOptions> _authOptions;
    private readonly IOptions<EmailConfirmationOptions> _emailConfirmationOptions;
    private readonly IEmailSender _emailSender;
    private readonly IPasswordHashService _passwordHashService;
    private readonly ISessionTokenService _sessionTokenService;
    private readonly IWorkspaceRepository _workspaceRepository;

    public AuthService(
        IAuthRepository authRepository,
        IAuthTokenAccessor authTokenAccessor,
        IClock clock,
        ICurrentUserSessionProvider currentUserSessionProvider,
        IOptions<AuthOptions> authOptions,
        IOptions<EmailConfirmationOptions> emailConfirmationOptions,
        IEmailSender emailSender,
        IPasswordHashService passwordHashService,
        ISessionTokenService sessionTokenService,
        IWorkspaceRepository workspaceRepository)
    {
        _authRepository = authRepository;
        _authTokenAccessor = authTokenAccessor;
        _clock = clock;
        _currentUserSessionProvider = currentUserSessionProvider;
        _authOptions = authOptions;
        _emailConfirmationOptions = emailConfirmationOptions;
        _emailSender = emailSender;
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
        var emailConfirmationIsEnabled = _emailConfirmationOptions.Value.Enabled;
        var created = await CreateUserWithWorkspaceAsync(
            request.Email,
            request.DisplayName,
            _passwordHashService.HashPassword(request.Password),
            now,
            emailIsConfirmed: !emailConfirmationIsEnabled,
            cancellationToken);

        if (emailConfirmationIsEnabled)
        {
            await CreateAndSendEmailConfirmationAsync(created.User, cancellationToken);
        }

        await _authRepository.SaveChangesAsync(cancellationToken);

        return new RegisterUserResponse(
            MapUser(created.User),
            MapWorkspace(created.Workspace, created.Membership),
            emailConfirmationIsEnabled);
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

        if (_emailConfirmationOptions.Value.Enabled && user.EmailConfirmedAt is null)
        {
            throw new EmailConfirmationRequiredException();
        }

        var (sessionToken, expiresAt) = await CreateSessionAsync(
            user,
            metadata,
            request.DeviceName,
            cancellationToken);
        await _authRepository.SaveChangesAsync(cancellationToken);

        var workspaces = await _authRepository.ListWorkspacesForUserAsync(user.Id, cancellationToken);

        return new LoginUserResponse(
            MapUser(user),
            workspaces.Select(MapWorkspace).ToList(),
            sessionToken,
            expiresAt);
    }

    public async Task<LoginUserResponse> DevelopmentLoginAsync(
        AuthRequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var options = _authOptions.Value;

        if (!options.EnableDevelopmentLogin)
        {
            throw new UnauthorizedAccessException("Development login is disabled.");
        }

        var normalizedEmail = AppUser.NormalizeEmail(options.DevelopmentEmail);
        var existingUser = await _authRepository.GetUserByNormalizedEmailAsync(
            normalizedEmail,
            trackChanges: false,
            cancellationToken);

        if (existingUser is null)
        {
            await RegisterAsync(
                new RegisterUserRequest(
                    options.DevelopmentEmail,
                    options.DevelopmentPassword,
                    options.DevelopmentDisplayName),
                cancellationToken);
        }

        return await LoginAsync(
            new LoginUserRequest(
                options.DevelopmentEmail,
                options.DevelopmentPassword,
                "development browser"),
            metadata,
            cancellationToken);
    }

    public async Task<LoginUserResponse> GuestLoginAsync(
        AuthRequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var options = _authOptions.Value;

        if (!options.AllowGuestSessions)
        {
            throw new UnauthorizedAccessException("Guest sessions are disabled.");
        }

        var now = _clock.UtcNow;
        var guestId = Guid.NewGuid().ToString("N");
        var created = await CreateUserWithWorkspaceAsync(
            $"guest-{guestId}@guest.dumptether.local",
            "Temporary user",
            _passwordHashService.HashPassword(_sessionTokenService.CreateSessionToken()),
            now,
            emailIsConfirmed: true,
            cancellationToken);
        var (sessionToken, expiresAt) = await CreateSessionAsync(
            created.User,
            metadata,
            "temporary browser tab",
            cancellationToken);

        await _authRepository.SaveChangesAsync(cancellationToken);

        return new LoginUserResponse(
            MapUser(created.User),
            [MapWorkspace(created.Workspace, created.Membership)],
            sessionToken,
            expiresAt);
    }

    public async Task<LoginUserResponse> ExternalLoginAsync(
        ExternalLoginRequest request,
        AuthRequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var provider = ExternalLogin.NormalizeProvider(request.Provider);
        var now = _clock.UtcNow;
        var externalLogin = await _authRepository.GetExternalLoginAsync(
            provider,
            request.ProviderUserId,
            trackChanges: true,
            cancellationToken);
        AppUser? user;

        if (externalLogin is not null)
        {
            user = await _authRepository.GetUserByIdAsync(
                externalLogin.UserId,
                trackChanges: true,
                cancellationToken);

            if (user is null || !user.IsActive)
            {
                throw new ValidationException("External login is not available.");
            }

            externalLogin.MarkUsed(now, request.Email);
        }
        else
        {
            user = await _authRepository.GetUserByNormalizedEmailAsync(
                AppUser.NormalizeEmail(request.Email),
                trackChanges: true,
                cancellationToken);

            if (user is null)
            {
                var created = await CreateUserWithWorkspaceAsync(
                    request.Email,
                    request.DisplayName,
                    _passwordHashService.HashPassword(_sessionTokenService.CreateSessionToken()),
                    now,
                    emailIsConfirmed: true,
                    cancellationToken);
                user = created.User;
            }
            else if (!user.IsActive)
            {
                throw new ValidationException("External login is not available.");
            }
            else
            {
                user.MarkEmailConfirmed(now);
            }

            externalLogin = ExternalLogin.Create(
                user.Id,
                provider,
                request.ProviderUserId,
                request.Email,
                now);
            await _authRepository.AddExternalLoginAsync(externalLogin, cancellationToken);
        }

        user.MarkEmailConfirmed(now);
        var (sessionToken, expiresAt) = await CreateSessionAsync(
            user,
            metadata,
            $"{provider} login",
            cancellationToken);
        await _authRepository.SaveChangesAsync(cancellationToken);

        var workspaces = await _authRepository.ListWorkspacesForUserAsync(user.Id, cancellationToken);

        return new LoginUserResponse(
            MapUser(user),
            workspaces.Select(MapWorkspace).ToList(),
            sessionToken,
            expiresAt);
    }

    public async Task<ConfirmEmailResponse> ConfirmEmailAsync(
        string token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ValidationException("Confirmation token is required.");
        }

        var now = _clock.UtcNow;
        var confirmationToken = await _authRepository.GetEmailConfirmationTokenByHashAsync(
            _sessionTokenService.HashToken(token),
            trackChanges: true,
            cancellationToken);

        if (confirmationToken is null || !confirmationToken.IsUsable(now))
        {
            throw new ValidationException("Confirmation token is invalid or expired.");
        }

        var user = await _authRepository.GetUserByIdAsync(
            confirmationToken.UserId,
            trackChanges: true,
            cancellationToken);

        if (user is null || !user.IsActive)
        {
            throw new ValidationException("Confirmation token is invalid or expired.");
        }

        user.MarkEmailConfirmed(now);
        confirmationToken.MarkUsed(now);
        await _authRepository.SaveChangesAsync(cancellationToken);

        return new ConfirmEmailResponse(user.Id, user.Email, now);
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
            user.LastLoginAt,
            user.EmailConfirmedAt);
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

    private async Task<(AppUser User, Workspace Workspace, WorkspaceMembership Membership)>
        CreateUserWithWorkspaceAsync(
            string email,
            string? displayName,
            string passwordHash,
            DateTimeOffset now,
            bool emailIsConfirmed,
            CancellationToken cancellationToken = default)
    {
        var user = AppUser.Create(
            email,
            displayName,
            passwordHash,
            now,
            emailIsConfirmed);
        var workspace = Workspace.Create("All Tasks", now);
        var membership = WorkspaceMembership.Create(
            workspace.Id,
            user.Id,
            WorkspaceMembershipRole.Owner,
            now);

        await _authRepository.AddUserAsync(user, cancellationToken);
        await _workspaceRepository.AddAsync(workspace, cancellationToken);
        await _authRepository.AddWorkspaceMembershipAsync(membership, cancellationToken);

        return (user, workspace, membership);
    }

    private async Task CreateAndSendEmailConfirmationAsync(
        AppUser user,
        CancellationToken cancellationToken)
    {
        var options = _emailConfirmationOptions.Value;
        var token = _sessionTokenService.CreateSessionToken();
        var now = _clock.UtcNow;
        var expiresAt = now.AddHours(Math.Max(1, options.TokenHours));
        var confirmationToken = EmailConfirmationToken.Create(
            user.Id,
            _sessionTokenService.HashToken(token),
            now,
            expiresAt);
        await _authRepository.AddEmailConfirmationTokenAsync(
            confirmationToken,
            cancellationToken);
        await _authRepository.SaveChangesAsync(cancellationToken);

        var confirmationLink = BuildConfirmationLink(options, token);
        var encodedLink = WebUtility.HtmlEncode(confirmationLink);
        await _emailSender.SendAsync(
            new EmailMessage(
                user.Email,
                user.DisplayName,
                "Confirm your DumpTether email",
                $"""
                <p>Welcome to DumpTether.</p>
                <p>Please confirm your email:</p>
                <p><a href="{encodedLink}">Confirm email</a></p>
                <p>This link expires in {Math.Max(1, options.TokenHours)} hours.</p>
                """,
                $"Confirm your DumpTether email: {confirmationLink}"),
            cancellationToken);
    }

    private static string BuildConfirmationLink(
        EmailConfirmationOptions options,
        string token)
    {
        var baseUrl = options.PublicBaseUrl.TrimEnd('/');
        var path = string.IsNullOrWhiteSpace(options.ConfirmPath)
            ? "/api/auth/confirm-email"
            : options.ConfirmPath.StartsWith('/')
                ? options.ConfirmPath
                : $"/{options.ConfirmPath}";

        return $"{baseUrl}{path}?token={Uri.EscapeDataString(token)}";
    }

    private async Task<(string SessionToken, DateTimeOffset ExpiresAt)> CreateSessionAsync(
        AppUser user,
        AuthRequestMetadata metadata,
        string? deviceName,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var sessionToken = _sessionTokenService.CreateSessionToken();
        var session = UserSession.Create(
            user.Id,
            _sessionTokenService.HashToken(sessionToken),
            now,
            now.Add(SessionDuration),
            metadata.UserAgent,
            _sessionTokenService.HashOptionalMetadata(metadata.IpAddress),
            deviceName);

        user.MarkLoggedIn(now);
        await _authRepository.AddSessionAsync(session, cancellationToken);

        return (sessionToken, session.ExpiresAt);
    }
}
