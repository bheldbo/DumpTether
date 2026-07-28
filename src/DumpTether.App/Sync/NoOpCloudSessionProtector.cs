namespace DumpTether.App.Sync;

internal sealed class NoOpCloudSessionProtector : ICloudSessionProtector
{
    public string Protect(string sessionToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            throw new ArgumentException("Cloud session token is required.", nameof(sessionToken));
        }

        return sessionToken.Trim();
    }

    public string Unprotect(string protectedSessionToken)
    {
        if (string.IsNullOrWhiteSpace(protectedSessionToken))
        {
            throw new ArgumentException("Protected cloud session token is required.", nameof(protectedSessionToken));
        }

        return protectedSessionToken.Trim();
    }
}
