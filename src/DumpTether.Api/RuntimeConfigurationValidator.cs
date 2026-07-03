using Microsoft.Extensions.Configuration;

namespace DumpTether.Api;

public static class RuntimeConfigurationValidator
{
    public static void Validate(IConfiguration configuration, bool isDevelopment)
    {
        var missingKeys = new List<string>();

        CorsConfiguration.ValidateAllowedOrigins(configuration);

        if (GetBoolean(configuration, "EmailConfirmation:Enabled"))
        {
            AddMissingBrevoApiKeys(configuration, missingKeys);
            AddIfMissing(configuration, "EmailConfirmation:PublicBaseUrl", missingKeys);
        }

        if (GetBoolean(configuration, "Mfa:Email:Enabled"))
        {
            AddMissingBrevoApiKeys(configuration, missingKeys);
        }

        if (GetBoolean(configuration, "Email:Smtp:Enabled"))
        {
            AddMissingEmailKeys(configuration, missingKeys);
        }

        if (GetBoolean(configuration, "Email:BrevoApi:Enabled"))
        {
            AddIfMissing(configuration, "Email:BrevoApi:ApiKey", missingKeys);
            AddIfMissing(configuration, "Email:FromEmail", missingKeys);
        }

        AddMissingOAuthKeys(configuration, "Google", missingKeys);
        AddMissingOAuthKeys(configuration, "Microsoft", missingKeys);
        AddMissingOAuthKeys(configuration, "Facebook", missingKeys);

        if (!isDevelopment &&
            GetBoolean(configuration, "Auth:EnableDevelopmentLogin"))
        {
            throw new InvalidOperationException(
                "Auth:EnableDevelopmentLogin must be false outside Development.");
        }

        AddMissingSignupKeys(configuration, missingKeys);

        if (missingKeys.Count > 0)
        {
            throw new InvalidOperationException(
                "DumpTether configuration is incomplete. Missing required setting(s): " +
                string.Join(", ", missingKeys.OrderBy(key => key)) +
                ". Configure them with environment variables or a local secret file; do not commit real secrets.");
        }
    }

    private static void AddMissingSignupKeys(
        IConfiguration configuration,
        List<string> missingKeys)
    {
        var signupMode = GetSignupMode(configuration);

        if (signupMode == "InviteOnly" &&
            !HasAnyValue(configuration.GetSection("Auth:SignupInviteCodes")))
        {
            missingKeys.Add("Auth:SignupInviteCodes");
        }

        if (signupMode == "Whitelist" &&
            !HasAnyValue(configuration.GetSection("Auth:SignupWhitelistEmails")) &&
            !HasAnyValue(configuration.GetSection("Auth:SignupWhitelistDomains")))
        {
            missingKeys.Add("Auth:SignupWhitelistEmails or Auth:SignupWhitelistDomains");
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

    private static void AddMissingBrevoApiKeys(
        IConfiguration configuration,
        List<string> missingKeys)
    {
        if (!GetBoolean(configuration, "Email:BrevoApi:Enabled"))
        {
            missingKeys.Add("Email:BrevoApi:Enabled");
        }

        AddIfMissing(configuration, "Email:BrevoApi:ApiKey", missingKeys);
        AddIfMissing(configuration, "Email:FromEmail", missingKeys);
    }

    private static void AddMissingOAuthKeys(
        IConfiguration configuration,
        string provider,
        List<string> missingKeys)
    {
        var section = $"OAuth:{provider}";

        if (!GetBoolean(configuration, $"{section}:Enabled"))
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

    private static bool GetBoolean(IConfiguration configuration, string key)
    {
        var value = configuration[key];

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException(
            $"DumpTether configuration value '{key}' must be 'true' or 'false', but was '{value}'. " +
            "Do not append inline comments after .env values; put comments on their own lines.");
    }

    private static string GetSignupMode(IConfiguration configuration)
    {
        var value = configuration["Auth:SignupMode"];

        if (string.IsNullOrWhiteSpace(value))
        {
            return "Open";
        }

        var normalized = value.Trim();
        if (normalized.Equals("Open", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Whitelist", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("InviteOnly", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("Closed", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        throw new InvalidOperationException(
            "DumpTether configuration value 'Auth:SignupMode' must be one of " +
            "Open, Whitelist, InviteOnly, or Closed.");
    }

    private static bool HasAnyValue(IConfigurationSection section) =>
        section.GetChildren().Any(child => !string.IsNullOrWhiteSpace(child.Value));

}
