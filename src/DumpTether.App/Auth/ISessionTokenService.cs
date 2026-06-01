namespace DumpTether.App.Auth;

public interface ISessionTokenService
{
    string CreateSessionToken();

    string HashToken(string token);

    string? HashOptionalMetadata(string? value);
}
