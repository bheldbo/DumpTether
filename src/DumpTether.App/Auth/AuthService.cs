using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using DumpTether.App.Email;
using DumpTether.App.Tasks;
using DumpTether.App.Workspaces;
using DumpTether.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DumpTether.App.Auth;

internal sealed class AuthService : IAuthService
{
    private const string LocalDesktopEmail = "local@desktop.dumptether.local";
    private const string LocalDesktopDisplayName = "Local user";

    private readonly IAuthRepository _authRepository;
    private readonly IAuthTokenAccessor _authTokenAccessor;
    private readonly IClock _clock;
    private readonly ICurrentUserSessionProvider _currentUserSessionProvider;
    private readonly IOptions<AuthOptions> _authOptions;
    private readonly IOptions<EmailConfirmationOptions> _emailConfirmationOptions;
    private readonly IOptions<LegalOptions> _legalOptions;
    private readonly IOptions<PasswordRecoveryOptions> _passwordRecoveryOptions;
    private readonly IOptions<AccountDeletionOptions> _accountDeletionOptions;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<AuthService> _logger;
    private readonly IPasswordHashService _passwordHashService;
    private readonly IRegistrationTransaction _registrationTransaction;
    private readonly ISessionTokenService _sessionTokenService;
    private readonly IWorkspaceRepository _workspaceRepository;

    public AuthService(
        IAuthRepository authRepository,
        IAuthTokenAccessor authTokenAccessor,
        IClock clock,
        ICurrentUserSessionProvider currentUserSessionProvider,
        IOptions<AuthOptions> authOptions,
        IOptions<EmailConfirmationOptions> emailConfirmationOptions,
        IOptions<LegalOptions> legalOptions,
        IOptions<PasswordRecoveryOptions> passwordRecoveryOptions,
        IOptions<AccountDeletionOptions> accountDeletionOptions,
        IEmailSender emailSender,
        ILogger<AuthService> logger,
        IPasswordHashService passwordHashService,
        IRegistrationTransaction registrationTransaction,
        ISessionTokenService sessionTokenService,
        IWorkspaceRepository workspaceRepository)
    {
        _authRepository = authRepository;
        _authTokenAccessor = authTokenAccessor;
        _clock = clock;
        _currentUserSessionProvider = currentUserSessionProvider;
        _authOptions = authOptions;
        _emailConfirmationOptions = emailConfirmationOptions;
        _legalOptions = legalOptions;
        _passwordRecoveryOptions = passwordRecoveryOptions;
        _accountDeletionOptions = accountDeletionOptions;
        _emailSender = emailSender;
        _logger = logger;
        _passwordHashService = passwordHashService;
        _registrationTransaction = registrationTransaction;
        _sessionTokenService = sessionTokenService;
        _workspaceRepository = workspaceRepository;
    }

    public async Task<RegisterUserResponse> RegisterAsync(
        RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedEmail = AppUser.NormalizeEmail(request.Email);
        EnsureSignupIsAllowed(normalizedEmail, request.InviteCode, allowInviteCode: true);
        ValidateRequiredLegalAcceptance(request.LegalAcceptance);

        return await _registrationTransaction.ExecuteAsync(
            async transactionCancellationToken =>
            {
                var existingUser = await _authRepository.GetUserByNormalizedEmailAsync(
                    normalizedEmail,
                    trackChanges: false,
                    transactionCancellationToken);

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
                    cancellationToken: transactionCancellationToken);
                var legalAcceptances = CreateRequiredLegalAcceptances(
                    created.User.Id,
                    request.LegalAcceptance,
                    now);
                if (legalAcceptances.Count > 0)
                {
                    await _authRepository.AddLegalAcceptancesAsync(
                        legalAcceptances,
                        transactionCancellationToken);
                }

                if (emailConfirmationIsEnabled)
                {
                    await CreateAndSendEmailConfirmationAsync(
                        created.User,
                        transactionCancellationToken);
                }
                else
                {
                    await _authRepository.SaveChangesAsync(transactionCancellationToken);
                }

                return new RegisterUserResponse(
                    MapUser(created.User),
                    MapWorkspace(created.Workspace, created.Membership),
                    emailConfirmationIsEnabled);
            },
            cancellationToken);
    }

    public async Task<LoginUserResponse> LoginAsync(
        LoginUserRequest request,
        AuthRequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        return await LoginCoreAsync(
            request,
            metadata,
            UserSessionType.Browser,
            cancellationToken);
    }

    public async Task<LoginUserResponse> DesktopCloudLoginAsync(
        LoginUserRequest request,
        AuthRequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        return await LoginCoreAsync(
            request,
            metadata,
            UserSessionType.DesktopCloud,
            cancellationToken);
    }

    private async Task<LoginUserResponse> LoginCoreAsync(
        LoginUserRequest request,
        AuthRequestMetadata metadata,
        UserSessionType sessionType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedEmail = AppUser.NormalizeEmail(request.Email);
        var user = await _authRepository.GetUserByNormalizedEmailAsync(
            normalizedEmail,
            trackChanges: true,
            cancellationToken);

        if (user is null)
        {
            LogAuthAuditEvent("login_failed_unknown_user", normalizedEmail, metadata);
            throw new ValidationException("Invalid email or password.");
        }

        if (!user.IsActive)
        {
            LogAuthAuditEvent("login_failed_inactive_user", normalizedEmail, metadata);
            throw new ValidationException("Invalid email or password.");
        }

        if (!_passwordHashService.VerifyPassword(user.PasswordHash, request.Password))
        {
            LogAuthAuditEvent("login_failed_bad_password", normalizedEmail, metadata);
            throw new ValidationException("Invalid email or password.");
        }

        if (_emailConfirmationOptions.Value.Enabled && user.EmailConfirmedAt is null)
        {
            LogAuthAuditEvent("login_failed_unconfirmed_email", normalizedEmail, metadata);
            throw new EmailConfirmationRequiredException();
        }

        var (sessionToken, session) = await CreateSessionAsync(
            user,
            metadata,
            sessionType,
            request.DeviceName,
            cancellationToken);
        await _authRepository.SaveChangesAsync(cancellationToken);

        var workspaces = await _authRepository.ListWorkspacesForUserAsync(user.Id, cancellationToken);

        return new LoginUserResponse(
            MapUser(user),
            workspaces.Select(MapWorkspace).ToList(),
            sessionToken,
            session.ExpiresAt,
            MapSession(session));
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
            var now = _clock.UtcNow;
            await CreateUserWithWorkspaceAsync(
                options.DevelopmentEmail,
                options.DevelopmentDisplayName,
                _passwordHashService.HashPassword(options.DevelopmentPassword),
                now,
                emailIsConfirmed: true,
                cancellationToken: cancellationToken);
            await _authRepository.SaveChangesAsync(cancellationToken);
        }

        return await LoginCoreAsync(
            new LoginUserRequest(
                options.DevelopmentEmail,
                options.DevelopmentPassword,
                "development browser"),
            metadata,
            UserSessionType.Development,
            cancellationToken);
    }

    public async Task<LoginUserResponse> LocalDesktopLoginAsync(
        AuthRequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var normalizedEmail = AppUser.NormalizeEmail(LocalDesktopEmail);
        var createdLocalUser = false;
        var user = await _authRepository.GetUserByNormalizedEmailAsync(
            normalizedEmail,
            trackChanges: true,
            cancellationToken);

        if (user is null)
        {
            var created = await CreateUserWithWorkspaceAsync(
                LocalDesktopEmail,
                LocalDesktopDisplayName,
                _passwordHashService.HashPassword(_sessionTokenService.CreateSessionToken()),
                now,
                emailIsConfirmed: true,
                hasPasswordCredential: false,
                cancellationToken);
            user = created.User;
            createdLocalUser = true;
        }
        else if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("Local desktop user is inactive.");
        }

        if (!createdLocalUser)
        {
            var existingWorkspaces = await _authRepository.ListWorkspacesForUserAsync(
                user.Id,
                cancellationToken);

            if (existingWorkspaces.Count == 0)
            {
                var workspace = Workspace.Create("All Tasks", now);
                var membership = WorkspaceMembership.Create(
                    workspace.Id,
                    user.Id,
                    WorkspaceMembershipRole.Owner,
                    now);
                await _workspaceRepository.AddAsync(workspace, cancellationToken);
                await _authRepository.AddWorkspaceMembershipAsync(membership, cancellationToken);
            }
        }

        var (sessionToken, session) = await CreateSessionAsync(
            user,
            metadata,
            UserSessionType.DesktopLocal,
            "desktop local app",
            cancellationToken);
        await _authRepository.SaveChangesAsync(cancellationToken);

        var workspaces = await _authRepository.ListWorkspacesForUserAsync(user.Id, cancellationToken);

        return new LoginUserResponse(
            MapUser(user),
            workspaces.Select(MapWorkspace).ToList(),
            sessionToken,
            session.ExpiresAt,
            MapSession(session));
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
            hasPasswordCredential: false,
            cancellationToken);
        var (sessionToken, session) = await CreateSessionAsync(
            created.User,
            metadata,
            UserSessionType.Guest,
            "temporary browser tab",
            cancellationToken);

        await _authRepository.SaveChangesAsync(cancellationToken);

        return new LoginUserResponse(
            MapUser(created.User),
            [MapWorkspace(created.Workspace, created.Membership)],
            sessionToken,
            session.ExpiresAt,
            MapSession(session));
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
                EnsureSignupIsAllowed(
                    AppUser.NormalizeEmail(request.Email),
                    inviteCode: null,
                    allowInviteCode: false);
                ValidateRequiredLegalAcceptance(request.LegalAcceptance);

                var created = await CreateUserWithWorkspaceAsync(
                    request.Email,
                    request.DisplayName,
                    _passwordHashService.HashPassword(_sessionTokenService.CreateSessionToken()),
                    now,
                    emailIsConfirmed: true,
                    hasPasswordCredential: false,
                    cancellationToken);
                user = created.User;
                var legalAcceptances = CreateRequiredLegalAcceptances(
                    user.Id,
                    request.LegalAcceptance,
                    now);
                if (legalAcceptances.Count > 0)
                {
                    await _authRepository.AddLegalAcceptancesAsync(
                        legalAcceptances,
                        cancellationToken);
                }
            }
            else if (!user.IsActive)
            {
                throw new ValidationException("External login is not available.");
            }
            else
            {
                throw new ValidationException(
                    "This external identity cannot be connected automatically. " +
                    "Sign in with your existing method; explicit account linking is not available yet.");
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
        var (sessionToken, session) = await CreateSessionAsync(
            user,
            metadata,
            UserSessionType.Browser,
            $"{provider} login",
            cancellationToken);
        await _authRepository.SaveChangesAsync(cancellationToken);

        var workspaces = await _authRepository.ListWorkspacesForUserAsync(user.Id, cancellationToken);

        return new LoginUserResponse(
            MapUser(user),
            workspaces.Select(MapWorkspace).ToList(),
            sessionToken,
            session.ExpiresAt,
            MapSession(session));
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

    public async Task RequestPasswordResetAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var normalizedEmail = AppUser.NormalizeEmail(request.Email);
        var user = await _authRepository.GetUserByNormalizedEmailAsync(
            normalizedEmail,
            trackChanges: false,
            cancellationToken);

        if (!_passwordRecoveryOptions.Value.Enabled ||
            user is null ||
            !user.IsActive ||
            user.EmailConfirmedAt is null ||
            !user.HasPasswordCredential)
        {
            _logger.LogInformation(
                "Password reset request completed without delivery. EmailHash: {EmailHash}.",
                _sessionTokenService.HashOptionalMetadata(normalizedEmail));
            return;
        }

        try
        {
            await _registrationTransaction.ExecuteAsync(
                async transactionCancellationToken =>
                {
                    await CreateAndSendPasswordResetAsync(user, transactionCancellationToken);
                    return true;
                },
                cancellationToken);
        }
        catch (EmailDeliveryException exception)
        {
            _logger.LogWarning(
                exception,
                "Password reset email delivery failed. UserId: {UserId}.",
                user.Id);
        }
    }

    public async Task SendPasswordResetForOperatorAsync(
        string email,
        string actor,
        string reason,
        CancellationToken cancellationToken)
    {
        if (!_passwordRecoveryOptions.Value.Enabled)
        {
            throw new ValidationException("Password recovery is not enabled.");
        }

        var user = await _authRepository.GetUserByNormalizedEmailAsync(
            AppUser.NormalizeEmail(email),
            trackChanges: false,
            cancellationToken);
        if (user is null ||
            !user.IsActive ||
            user.EmailConfirmedAt is null ||
            !user.HasPasswordCredential)
        {
            throw new ValidationException("Account is not eligible for password recovery.");
        }

        var auditEvent = OperatorAuditEvent.Create(
            actor,
            "password_reset.requested",
            user.Id,
            user.Email,
            reason,
            _clock.UtcNow);
        await _registrationTransaction.ExecuteAsync(
            async transactionCancellationToken =>
            {
                await CreateAndSendPasswordResetAsync(
                    user,
                    transactionCancellationToken,
                    auditEvent);
                return true;
            },
            cancellationToken);
    }

    public async Task ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            throw new ValidationException("Password reset token is invalid or expired.");
        }

        await _registrationTransaction.ExecuteAsync(
            async transactionCancellationToken =>
            {
                var now = _clock.UtcNow;
                var resetToken = await _authRepository.GetPasswordResetTokenByHashAsync(
                    _sessionTokenService.HashToken(request.Token),
                    trackChanges: false,
                    transactionCancellationToken);
                if (resetToken is null || !resetToken.IsUsable(now))
                {
                    throw new ValidationException("Password reset token is invalid or expired.");
                }

                var user = await _authRepository.GetUserByIdAsync(
                    resetToken.UserId,
                    trackChanges: true,
                    transactionCancellationToken);
                if (user is null || !user.IsActive || !user.HasPasswordCredential)
                {
                    throw new ValidationException("Password reset token is invalid or expired.");
                }

                var consumed = await _authRepository.TryConsumePasswordResetTokenAsync(
                    resetToken.Id,
                    now,
                    transactionCancellationToken);
                if (!consumed)
                {
                    throw new ValidationException("Password reset token is invalid or expired.");
                }

                await _authRepository.InvalidatePasswordResetTokensForUserAsync(
                    user.Id,
                    resetToken.Id,
                    now,
                    transactionCancellationToken);
                user.ChangePassword(_passwordHashService.HashPassword(request.NewPassword), now);
                await _authRepository.RevokeSessionsForUserAsync(
                    user.Id,
                    now,
                    transactionCancellationToken);
                await _authRepository.SaveChangesAsync(transactionCancellationToken);
                _logger.LogInformation(
                    "Password reset completed and existing sessions were revoked. UserId: {UserId}.",
                    user.Id);
                return true;
            },
            cancellationToken);
    }

    public async Task<AccountDeletionStatusResponse?> GetAccountDeletionStatusAsync(
        CancellationToken cancellationToken)
    {
        var current = await _currentUserSessionProvider.GetCurrentAsync(cancellationToken) ??
            throw new UnauthorizedAccessException("Authentication is required.");
        var deletionRequest = await _authRepository.GetAccountDeletionRequestForUserAsync(
            current.UserId,
            trackChanges: false,
            cancellationToken);
        return MapDeletionStatus(deletionRequest);
    }

    public async Task<AccountDeletionStatusResponse> RequestAccountDeletionAsync(
        RequestAccountDeletionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_accountDeletionOptions.Value.Enabled)
        {
            throw new ValidationException("Account deletion is not available.");
        }

        var current = await _currentUserSessionProvider.GetCurrentAsync(cancellationToken) ??
            throw new UnauthorizedAccessException("Authentication is required.");
        if (current.SessionType == UserSessionType.DesktopLocal)
        {
            throw new ValidationException("The local desktop profile stays on this device and cannot be deleted here.");
        }

        var user = await _authRepository.GetUserByIdAsync(
                current.UserId,
                trackChanges: true,
                cancellationToken) ??
            throw new UnauthorizedAccessException("Authentication is required.");
        if (!string.Equals(
                user.NormalizedEmail,
                AppUser.NormalizeEmail(request.ConfirmationEmail),
                StringComparison.Ordinal))
        {
            throw new ValidationException("Enter the account email exactly to schedule deletion.");
        }

        if (user.HasPasswordCredential)
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
                !_passwordHashService.VerifyPassword(user.PasswordHash, request.CurrentPassword))
            {
                throw new ValidationException("Enter your current password to schedule account deletion.");
            }
        }
        else
        {
            var recentAuthenticationWindow = TimeSpan.FromMinutes(
                Math.Clamp(_accountDeletionOptions.Value.RecentAuthenticationMinutes, 1, 60));
            if (current.CreatedAt < _clock.UtcNow.Subtract(recentAuthenticationWindow))
            {
                throw new ValidationException("Sign in again with Microsoft before scheduling account deletion.");
            }
        }

        if (await _authRepository.HasOwnedWorkspaceSharedWithOthersAsync(user.Id, cancellationToken))
        {
            throw new ValidationException(
                "Remove board members, pending invitations and task shares before deleting this account.");
        }

        var existingRequest = await _authRepository.GetAccountDeletionRequestForUserAsync(
            user.Id,
            trackChanges: false,
            cancellationToken);
        if (existingRequest is not null)
        {
            return MapDeletionStatus(existingRequest)!;
        }

        var now = _clock.UtcNow;
        var graceHours = Math.Clamp(_accountDeletionOptions.Value.GraceHours, 24, 720);
        var scheduledFor = now.AddHours(graceHours);
        var reminderHoursBefore = Math.Clamp(
            _accountDeletionOptions.Value.ReminderHoursBefore,
            1,
            graceHours - 1);
        var deletionRequest = AccountDeletionRequest.Create(
            user.Id,
            now,
            scheduledFor.AddHours(-reminderHoursBefore),
            scheduledFor);
        await _authRepository.AddAccountDeletionRequestAsync(deletionRequest, cancellationToken);
        await _authRepository.SaveChangesAsync(cancellationToken);
        try
        {
            await _emailSender.SendAsync(
                AccountEmailBuilders.AccountDeletionScheduled(user.Email, user.DisplayName, scheduledFor),
                cancellationToken);
        }
        catch (EmailDeliveryException exception)
        {
            _logger.LogWarning(
                exception,
                "Account deletion schedule email delivery failed. UserId: {UserId}.",
                user.Id);
        }
        return MapDeletionStatus(deletionRequest)!;
    }

    public async Task<bool> CancelAccountDeletionAsync(CancellationToken cancellationToken)
    {
        var current = await _currentUserSessionProvider.GetCurrentAsync(cancellationToken) ??
            throw new UnauthorizedAccessException("Authentication is required.");
        var deletionRequest = await _authRepository.GetAccountDeletionRequestForUserAsync(
            current.UserId,
            trackChanges: true,
            cancellationToken);
        if (deletionRequest is null || deletionRequest.State != AccountDeletionRequestState.Pending)
        {
            return false;
        }

        _authRepository.RemoveAccountDeletionRequest(deletionRequest);
        await _authRepository.SaveChangesAsync(cancellationToken);
        return true;
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

        if (session.SessionType == UserSessionType.DesktopLocal)
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
            workspaces.Select(MapWorkspace).ToList(),
            new AuthSessionResponse(
                current.SessionId,
                current.SessionType,
                current.DeviceName,
                current.CreatedAt,
                current.ExpiresAt,
                current.LastSeenAt));
    }

    public async Task<IReadOnlyList<AuthSessionListItemResponse>> ListSessionsAsync(
        CancellationToken cancellationToken)
    {
        var current = await _currentUserSessionProvider.GetCurrentAsync(cancellationToken);

        if (current is null)
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        var sessions = await _authRepository.ListSessionsForUserAsync(
            current.UserId,
            cancellationToken);

        return sessions
            .Select(session => MapSessionListItem(session, current.SessionId))
            .ToList();
    }

    public async Task<RevokeAuthSessionResponse> RevokeSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var current = await _currentUserSessionProvider.GetCurrentAsync(cancellationToken);

        if (current is null)
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        var session = await _authRepository.GetSessionByIdAsync(
            sessionId,
            trackChanges: true,
            cancellationToken);

        if (session is null || session.UserId != current.UserId)
        {
            return new RevokeAuthSessionResponse(false, false);
        }

        if (session.SessionType == UserSessionType.DesktopLocal)
        {
            return new RevokeAuthSessionResponse(false, false);
        }

        var currentSessionRevoked = session.Id == current.SessionId;
        session.Revoke(_clock.UtcNow);
        await _authRepository.SaveChangesAsync(cancellationToken);

        return new RevokeAuthSessionResponse(true, currentSessionRevoked);
    }

    private static AuthUserResponse MapUser(AppUser user)
    {
        return new AuthUserResponse(
            user.Id,
            user.Email,
            user.DisplayName,
            user.CreatedAt,
            user.LastLoginAt,
            user.EmailConfirmedAt,
            user.HasPasswordCredential);
    }

    private static AuthWorkspaceResponse MapWorkspace(UserWorkspaceMembership membership)
    {
        return MapWorkspace(
            membership.Workspace,
            membership.Membership,
            membership.AccessKind,
            membership.SharedTaskCount);
    }

    private static AuthWorkspaceResponse MapWorkspace(
        Workspace workspace,
        WorkspaceMembership membership,
        string accessKind = WorkspaceAccessKinds.Membership,
        int sharedTaskCount = 0)
    {
        return new AuthWorkspaceResponse(
            workspace.Id,
            workspace.Name,
            workspace.Color,
            membership.Role,
            accessKind,
            sharedTaskCount);
    }

    private static AuthSessionResponse MapSession(UserSession session)
    {
        return new AuthSessionResponse(
            session.Id,
            session.SessionType,
            session.DeviceName,
            session.CreatedAt,
            session.ExpiresAt,
            session.LastSeenAt);
    }

    private static AuthSessionListItemResponse MapSessionListItem(
        UserSession session,
        Guid currentSessionId)
    {
        return new AuthSessionListItemResponse(
            session.Id,
            session.SessionType,
            session.DeviceName,
            session.CreatedAt,
            session.ExpiresAt,
            session.LastSeenAt,
            session.RevokedAt,
            session.Id == currentSessionId);
    }

    private async Task<(AppUser User, Workspace Workspace, WorkspaceMembership Membership)>
        CreateUserWithWorkspaceAsync(
            string email,
            string? displayName,
            string passwordHash,
            DateTimeOffset now,
            bool emailIsConfirmed,
            bool hasPasswordCredential = true,
            CancellationToken cancellationToken = default)
    {
        var user = AppUser.Create(
            email,
            displayName,
            passwordHash,
            now,
            emailIsConfirmed,
            hasPasswordCredential);
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

    private void EnsureSignupIsAllowed(
        string normalizedEmail,
        string? inviteCode,
        bool allowInviteCode)
    {
        var options = _authOptions.Value;

        switch (options.SignupMode)
        {
            case AuthSignupMode.Open:
                return;
            case AuthSignupMode.Whitelist:
                if (EmailIsWhitelisted(normalizedEmail, options))
                {
                    return;
                }

                LogSignupAuditEvent("signup_rejected_not_whitelisted", normalizedEmail);
                throw new ValidationException("Registration is not available for this email.");
            case AuthSignupMode.InviteOnly:
                if (allowInviteCode &&
                    InviteCodeMatches(inviteCode, options.SignupInviteCodes))
                {
                    return;
                }

                LogSignupAuditEvent("signup_rejected_invite_required", normalizedEmail);
                throw new ValidationException("A valid invite code is required.");
            case AuthSignupMode.Closed:
                LogSignupAuditEvent("signup_rejected_closed", normalizedEmail);
                throw new ValidationException("Registration is closed on this server.");
            default:
                LogSignupAuditEvent("signup_rejected_invalid_mode", normalizedEmail);
                throw new ValidationException("Registration is not available.");
        }
    }

    private IReadOnlyCollection<LegalAcceptance> CreateRequiredLegalAcceptances(
        Guid userId,
        LegalAcceptanceSubmission? submission,
        DateTimeOffset acceptedAt)
    {
        var options = _legalOptions.Value;
        if (!options.RequireAcceptance)
        {
            return [];
        }

        ValidateRequiredLegalAcceptance(submission);

        return
        [
            LegalAcceptance.Create(
                userId,
                LegalDocumentType.TermsOfUse,
                options.TermsVersion,
                acceptedAt),
            LegalAcceptance.Create(
                userId,
                LegalDocumentType.PrivacyNotice,
                options.PrivacyNoticeVersion,
                acceptedAt)
        ];
    }

    private void ValidateRequiredLegalAcceptance(LegalAcceptanceSubmission? submission)
    {
        var options = _legalOptions.Value;
        if (!options.RequireAcceptance)
        {
            return;
        }

        if (submission is null ||
            !submission.TermsAccepted ||
            !submission.PrivacyNoticeAcknowledged)
        {
            throw new ValidationException(
                "The current Terms of Use must be accepted and the Privacy Notice acknowledged.");
        }

        if (!string.Equals(
                submission.TermsVersion?.Trim(),
                options.TermsVersion.Trim(),
                StringComparison.Ordinal) ||
            !string.Equals(
                submission.PrivacyNoticeVersion?.Trim(),
                options.PrivacyNoticeVersion.Trim(),
                StringComparison.Ordinal))
        {
            throw new ValidationException(
                "The legal documents changed. Review the current versions before creating an account.");
        }
    }

    private static bool EmailIsWhitelisted(string normalizedEmail, AuthOptions options)
    {
        if (options.SignupWhitelistEmails
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Any(email =>
                string.Equals(
                    AppUser.NormalizeEmail(email),
                    normalizedEmail,
                    StringComparison.Ordinal)))
        {
            return true;
        }

        var atIndex = normalizedEmail.LastIndexOf('@');
        if (atIndex < 0 || atIndex == normalizedEmail.Length - 1)
        {
            return false;
        }

        var domain = normalizedEmail[(atIndex + 1)..];
        return options.SignupWhitelistDomains.Any(candidate =>
        {
            var normalizedDomain = candidate.Trim().TrimStart('@').ToUpperInvariant();
            return normalizedDomain.Length > 0 &&
                string.Equals(normalizedDomain, domain, StringComparison.Ordinal);
        });
    }

    private static bool InviteCodeMatches(string? inviteCode, IEnumerable<string> configuredCodes)
    {
        var trimmedInviteCode = inviteCode?.Trim();

        if (string.IsNullOrWhiteSpace(trimmedInviteCode))
        {
            return false;
        }

        var inviteCodeHash = Sha256(trimmedInviteCode);
        return configuredCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => Sha256(code.Trim()))
            .Any(hash => CryptographicOperations.FixedTimeEquals(inviteCodeHash, hash));
    }

    private static byte[] Sha256(string value) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(value));

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
        await _emailSender.SendAsync(
            EmailConfirmationEmailBuilder.Build(
                user.Email,
                user.DisplayName,
                confirmationLink,
                options.TokenHours),
            cancellationToken);
    }

    private async Task CreateAndSendPasswordResetAsync(
        AppUser user,
        CancellationToken cancellationToken,
        OperatorAuditEvent? auditEvent = null)
    {
        var options = _passwordRecoveryOptions.Value;
        var rawToken = _sessionTokenService.CreateSessionToken();
        var now = _clock.UtcNow;
        var tokenHours = Math.Clamp(options.TokenHours, 1, 24);
        await _authRepository.AddPasswordResetTokenAsync(
            PasswordResetToken.Create(
                user.Id,
                _sessionTokenService.HashToken(rawToken),
                now,
                now.AddHours(tokenHours)),
            cancellationToken);
        if (auditEvent is not null)
        {
            await _authRepository.AddOperatorAuditEventAsync(auditEvent, cancellationToken);
        }

        await _authRepository.SaveChangesAsync(cancellationToken);
        await _emailSender.SendAsync(
            AccountEmailBuilders.PasswordReset(
                user.Email,
                user.DisplayName,
                BuildPasswordResetLink(options, rawToken),
                tokenHours),
            cancellationToken);
    }

    private static string BuildPasswordResetLink(
        PasswordRecoveryOptions options,
        string token)
    {
        var baseUrl = options.PublicBaseUrl.TrimEnd('/');
        var path = string.IsNullOrWhiteSpace(options.ResetPath)
            ? "/#reset-password="
            : options.ResetPath;
        return path.Contains("{token}", StringComparison.Ordinal)
            ? $"{baseUrl}{path.Replace("{token}", Uri.EscapeDataString(token), StringComparison.Ordinal)}"
            : $"{baseUrl}{path}{Uri.EscapeDataString(token)}";
    }

    private async Task<AppUser> GetCurrentUserForUpdateAsync(CancellationToken cancellationToken)
    {
        var current = await _currentUserSessionProvider.GetCurrentAsync(cancellationToken) ??
            throw new UnauthorizedAccessException("Authentication is required.");
        return await _authRepository.GetUserByIdAsync(
                current.UserId,
                trackChanges: true,
                cancellationToken) ??
            throw new UnauthorizedAccessException("Authentication is required.");
    }

    private static AccountDeletionStatusResponse? MapDeletionStatus(AccountDeletionRequest? request) =>
        request is null
            ? null
            : new AccountDeletionStatusResponse(
                request.RequestedAt,
                request.ScheduledFor,
                request.ReminderSentAt);

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

    private async Task<(string SessionToken, UserSession Session)> CreateSessionAsync(
        AppUser user,
        AuthRequestMetadata metadata,
        UserSessionType sessionType,
        string? deviceName,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        await CleanupInactiveSessionsAsync(now, cancellationToken);

        var sessionToken = _sessionTokenService.CreateSessionToken();
        var session = UserSession.Create(
            user.Id,
            _sessionTokenService.HashToken(sessionToken),
            now,
            now.Add(GetSessionDuration()),
            sessionType,
            metadata.UserAgent,
            _sessionTokenService.HashOptionalMetadata(metadata.IpAddress),
            deviceName);

        user.MarkLoggedIn(now);
        await _authRepository.AddSessionAsync(session, cancellationToken);

        return (sessionToken, session);
    }

    private TimeSpan GetSessionDuration()
    {
        var sessionDays = Math.Clamp(_authOptions.Value.SessionDays, 1, 365);
        return TimeSpan.FromDays(sessionDays);
    }

    private async Task CleanupInactiveSessionsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var cleanupDays = _authOptions.Value.SessionCleanupDays;

        if (cleanupDays <= 0)
        {
            return;
        }

        var boundedCleanupDays = Math.Clamp(cleanupDays, 1, 3650);
        await _authRepository.DeleteInactiveSessionsAsync(
            now,
            now.AddDays(-boundedCleanupDays),
            cancellationToken);
    }

    private void LogAuthAuditEvent(
        string eventName,
        string normalizedEmail,
        AuthRequestMetadata metadata)
    {
        _logger.LogWarning(
            "Auth audit event {EventName}. EmailHash: {EmailHash}. IpHash: {IpHash}. UserAgentLength: {UserAgentLength}.",
            eventName,
            _sessionTokenService.HashOptionalMetadata(normalizedEmail),
            _sessionTokenService.HashOptionalMetadata(metadata.IpAddress),
            metadata.UserAgent?.Length ?? 0);
    }

    private void LogSignupAuditEvent(
        string eventName,
        string normalizedEmail)
    {
        _logger.LogWarning(
            "Auth audit event {EventName}. EmailHash: {EmailHash}.",
            eventName,
            _sessionTokenService.HashOptionalMetadata(normalizedEmail));
    }
}
