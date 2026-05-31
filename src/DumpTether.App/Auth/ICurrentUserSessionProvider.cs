namespace DumpTether.App.Auth;

public interface ICurrentUserSessionProvider
{
    Task<CurrentUserSession?> GetCurrentAsync(CancellationToken cancellationToken);
}
