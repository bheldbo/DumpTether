namespace DumpTether.App.Sync;

public interface ICloudSessionProtector
{
    string Protect(string sessionToken);

    string Unprotect(string protectedSessionToken);
}
