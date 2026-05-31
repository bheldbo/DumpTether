using DumpTether.App.Tasks;

namespace DumpTether.App.Auth;

internal sealed class CurrentUserSessionProvider : ICurrentUserSessionProvider
{
    private readonly IAuthRepository _authRepository;
    private readonly IAuthTokenAccessor _authTokenAccessor;
    private readonly IClock _clock;
    private readonly ISessionTokenService _sessionTokenService;

    public CurrentUserSessionProvider(
        IAuthRepository authRepository,
        IAuthTokenAccessor authTokenAccessor,
        IClock clock,
        ISessionTokenService sessionTokenService)
    {
        _authRepository = authRepository;
        _authTokenAccessor = authTokenAccessor;
        _clock = clock;
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

        if (session is null || !session.IsUsable(_clock.UtcNow))
        {
            return null;
        }

        var user = await _authRepository.GetUserByIdAsync(
            session.UserId,
            trackChanges: false,
            cancellationToken);

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
}
