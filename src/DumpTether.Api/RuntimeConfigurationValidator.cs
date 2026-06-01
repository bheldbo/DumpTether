using Microsoft.Extensions.Configuration;

namespace DumpTether.Api;

public static class RuntimeConfigurationValidator
{
    public static void Validate(IConfiguration configuration, bool isDevelopment)
    {
        var missingKeys = new List<string>();

        if (configuration.GetValue<bool>("EmailConfirmation:Enabled") ||
            configuration.GetValue<bool>("Mfa:Email:Enabled"))
        {
            AddMissingEmailKeys(configuration, missingKeys);
        }

        if (configuration.GetValue<bool>("Email:Smtp:Enabled"))
        {
            AddMissingEmailKeys(configuration, missingKeys);
        }

        if (configuration.GetValue<bool>("Email:BrevoApi:Enabled"))
        {
            AddIfMissing(configuration, "Email:BrevoApi:ApiKey", missingKeys);
            AddIfMissing(configuration, "Email:FromEmail", missingKeys);
        }

        AddMissingOAuthKeys(configuration, "Google", missingKeys);
        AddMissingOAuthKeys(configuration, "Microsoft", missingKeys);

        if (!isDevelopment &&
            configuration.GetValue<bool>("Auth:EnableDevelopmentLogin"))
        {
            throw new InvalidOperationException(
                "Auth:EnableDevelopmentLogin must be false outside Development.");
        }

        if (missingKeys.Count > 0)
        {
            throw new InvalidOperationException(
                "DumpTether configuration is incomplete. Missing required setting(s): " +
                string.Join(", ", missingKeys.OrderBy(key => key)) +
                ". Configure them with environment variables or a local secret file; do not commit real secrets.");
        }
    }

    private static void AddMissingEmailKeys(
        IConfiguration configuration,
        List<string> missingKeys)
    {
        AddIfMissing(configuration, "Email:FromEmail", missingKeys);
        AddIfMissing(configuration, "Email:Smtp:Host", missingKeys);
        AddIfMissing(configuration, "Email:Smtp:Port", missingKeys);
        AddIfMissing(configuration, "Email:Smtp:Username", missingKeys);
        AddIfMissing(configuration, "Email:Smtp:Password", missingKeys);
    }

    private static void AddMissingOAuthKeys(
        IConfiguration configuration,
        string provider,
        List<string> missingKeys)
    {
        var section = $"OAuth:{provider}";

        if (!configuration.GetValue<bool>($"{section}:Enabled"))
        {
            return;
        }

        AddIfMissing(configuration, $"{section}:ClientId", missingKeys);
        AddIfMissing(configuration, $"{section}:ClientSecret", missingKeys);
        AddIfMissing(configuration, $"{section}:Authority", missingKeys);
    }

    private static void AddIfMissing(
        IConfiguration configuration,
        string key,
        List<string> missingKeys)
    {
        if (string.IsNullOrWhiteSpace(configuration[key]))
        {
            missingKeys.Add(key);
        }
    }
}
