namespace DumpTether.App.Auth;

public sealed class OAuthOptions
{
    public OAuthProviderOptions Google { get; set; } = new()
    {
        Authority = "https://accounts.google.com"
    };

    public OAuthProviderOptions Microsoft { get; set; } = new()
    {
        Authority = "https://login.microsoftonline.com/common/v2.0"
    };

    public OAuthProviderOptions Facebook { get; set; } = new()
    {
        Authority = "https://www.facebook.com"
    };

    public IReadOnlyList<string> EnabledProviders()
    {
        var providers = new List<string>();

        if (Google.Enabled)
        {
            providers.Add("google");
        }

        if (Microsoft.Enabled)
        {
            providers.Add("microsoft");
        }

        if (Facebook.Enabled)
        {
            providers.Add("facebook");
        }

        return providers;
    }
}

public sealed class OAuthProviderOptions
{
    public bool Enabled { get; set; }

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string Authority { get; set; } = string.Empty;
}
