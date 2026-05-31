using System.Security.Cryptography;
using System.Text;

namespace DumpTether.App.Auth;

internal sealed class SessionTokenService : ISessionTokenService
{
    public string CreateSessionToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public string HashToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Token cannot be empty.", nameof(token));
        }

        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim())));
    }

    public string? HashOptionalMetadata(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim())));
    }
}
