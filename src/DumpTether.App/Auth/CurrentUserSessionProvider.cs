using DumpTether.App.Tasks;
using Microsoft.Extensions.Logging;

namespace DumpTether.App.Auth;

internal sealed class CurrentUserSessionProvider : ICurrentUserSessionProvider
{
    private readonly IAuthRepository _authRepository;
    private readonly IAuthTokenAccessor _authTokenAccessor;
    private readonly IClock _clock;
    private readonly ILogger<CurrentUserSessionProvider> _logger;
    private readonly ISessionTokenService _sessionTokenService;

    public CurrentUserSessionProvider(
        IAuthRepository authRepository,
        IAuthTokenAccessor authTokenAccessor,
        IClock clock,
        ILogger<CurrentUserSessionProvider> logger,
        ISessionTokenService sessionTokenService)
    {
        _authRepository = authRepository;
        _authTokenAccessor = authTokenAccessor;
        _clock = clock;
        _logger = logger;
        _sessionTokenService = sessionTokenService;
    }

    public async Task<CurrentUserSession?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var token = _authTokenAccessor.SessionToken;

        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var session = await _authRepository.GetSessionByTokenHashAsync(
            _sessionTokenService.HashToken(token),
            trackChanges: false,
            cancellationToken);

        if (session is null)
        {
            _logger.LogDebug("Auth audit event session_token_not_found.");
            return null;
        }

        if (!session.IsUsable(_clock.UtcNow))
        {
            _logger.LogWarning(
                "Auth audit event session_not_usable. SessionId: {SessionId}. UserId: {UserId}. Revoked: {Revoked}.",
                session.Id,
                session.UserId,
                session.RevokedAt.HasValue);
            return null;
        }

        var user = await _authRepository.GetUserByIdAsync(
            session.UserId,
            trackChanges: false,
            cancellationToken);

        if (user is null)
        {
            _logger.LogWarning(
                "Auth audit event session_user_not_found. SessionId: {SessionId}. UserId: {UserId}.",
                session.Id,
                session.UserId);
            return null;
        }

        if (!user.IsActive)
        {
            _logger.LogWarning(
                "Auth audit event session_inactive_user. SessionId: {SessionId}. UserId: {UserId}.",
                session.Id,
                session.UserId);
            return null;
        }

        return new CurrentUserSession(
            user.Id,
            session.Id,
            user.Email,
            user.DisplayName,
            session.SessionType,
            session.DeviceName,
            session.CreatedAt,
            session.ExpiresAt,
            session.LastSeenAt);
    }
}
