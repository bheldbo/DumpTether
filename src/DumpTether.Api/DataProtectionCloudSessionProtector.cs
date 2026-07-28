using DumpTether.App.Sync;
using Microsoft.AspNetCore.DataProtection;

namespace DumpTether.Api;

internal sealed class DataProtectionCloudSessionProtector : ICloudSessionProtector
{
    private const string Purpose = "DumpTether.CloudSync.SessionToken.v1";
    private readonly IDataProtector _protector;

    public DataProtectionCloudSessionProtector(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
    }

    public string Protect(string sessionToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            throw new ArgumentException("Cloud session token is required.", nameof(sessionToken));
        }

        return _protector.Protect(sessionToken.Trim());
    }

    public string Unprotect(string protectedSessionToken)
    {
        if (string.IsNullOrWhiteSpace(protectedSessionToken))
        {
            throw new ArgumentException("Protected cloud session token is required.", nameof(protectedSessionToken));
        }

        return _protector.Unprotect(protectedSessionToken.Trim());
    }
}
