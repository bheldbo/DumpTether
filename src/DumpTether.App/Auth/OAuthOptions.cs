namespace DumpTether.App.Auth;

public sealed class OAuthOptions
{
    public OAuthProviderOptions Microsoft { get; set; } = new();

    public IReadOnlyList<string> EnabledProviders() =>
        Microsoft.Enabled ? ["microsoft"] : [];
}

public sealed class OAuthProviderOptions
{
    public bool Enabled { get; set; }

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;

    public string TenantId { get; set; } = "common";
}
