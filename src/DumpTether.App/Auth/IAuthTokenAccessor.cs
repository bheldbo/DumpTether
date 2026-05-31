namespace DumpTether.App.Auth;

public interface IAuthTokenAccessor
{
    string? SessionToken { get; }
}
