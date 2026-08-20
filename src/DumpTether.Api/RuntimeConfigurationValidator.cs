using Microsoft.Extensions.Configuration;
using DumpTether.App.Email;

namespace DumpTether.Api;

public static class RuntimeConfigurationValidator
{
    public static void Validate(
        IConfiguration configuration,
        bool isDevelopment,
        bool isDesktop = false)
    {
        var missingKeys = new List<string>();

        CorsConfiguration.ValidateAllowedOrigins(configuration);
        if (isDesktop)
        {
            ValidateDesktopRuntime(configuration);
        }

        var emailProvider = GetEmailProvider(configuration);

        if (GetBoolean(configuration, "EmailConfirmation:Enabled"))
        {
            AddMissingEmailProviderKeys(configuration, emailProvider, missingKeys);
            AddIfMissing(configuration, "EmailConfirmation:PublicBaseUrl", missingKeys);
        }

        if (GetBoolean(configuration, "PasswordRecovery:Enabled"))
        {
            AddMissingEmailProviderKeys(configuration, emailProvider, missingKeys);
            AddIfMissing(configuration, "PasswordRecovery:PublicBaseUrl", missingKeys);
        }

        if (GetBoolean(configuration, "AccountDeletion:Enabled"))
        {
            AddMissingEmailProviderKeys(configuration, emailProvider, missingKeys);
        }

        if (GetBoolean(configuration, "Notifications:Enabled"))
        {
            AddMissingEmailProviderKeys(configuration, emailProvider, missingKeys);
        }

        if (GetBoolean(configuration, "Mfa:Email:Enabled"))
        {
            throw new InvalidOperationException(
                "Email MFA is not implemented yet. Set Mfa:Email:Enabled to false. " +
                "Email confirmation remains available independently.");
        }

        if (emailProvider == EmailProvider.Smtp)
        {
            AddMissingSmtpKeys(configuration, missingKeys);
        }

        if (emailProvider == EmailProvider.BrevoApi)
        {
            AddMissingBrevoApiKeys(configuration, missingKeys);
        }

        AddMissingOAuthKeys(configuration, "Microsoft", missingKeys);
        AddMissingLegalKeys(configuration, missingKeys);

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
                string.Join(", ", missingKeys.Distinct().OrderBy(key => key)) +
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

    private static void AddMissingLegalKeys(
        IConfiguration configuration,
        List<string> missingKeys)
    {
        if (!GetBoolean(configuration, "Legal:RequireAcceptance"))
        {
            return;
        }

        AddIfMissing(configuration, "Legal:TermsVersion", missingKeys);
        AddIfMissing(configuration, "Legal:PrivacyNoticeVersion", missingKeys);
        AddIfMissing(configuration, "Legal:OperatorName", missingKeys);
        AddIfMissing(configuration, "Legal:PrivacyContactEmail", missingKeys);
    }

    private static void AddMissingSmtpKeys(
        IConfiguration configuration,
        List<string> missingKeys)
    {
        AddIfMissing(configuration, "Email:FromEmail", missingKeys);
        AddIfMissing(configuration, "Email:Smtp:Host", missingKeys);
        AddIfMissing(configuration, "Email:Smtp:Port", missingKeys);

        if (GetBoolean(configuration, "Email:Smtp:UseAuthentication"))
        {
            AddIfMissing(configuration, "Email:Smtp:Username", missingKeys);
            AddIfMissing(configuration, "Email:Smtp:Password", missingKeys);
        }
    }

    private static void AddMissingBrevoApiKeys(
        IConfiguration configuration,
        List<string> missingKeys)
    {
        AddIfMissing(configuration, "Email:BrevoApi:ApiKey", missingKeys);
        AddIfMissing(configuration, "Email:FromEmail", missingKeys);
    }

    private static void AddMissingEmailProviderKeys(
        IConfiguration configuration,
        EmailProvider provider,
        List<string> missingKeys)
    {
        if (provider == EmailProvider.None)
        {
            missingKeys.Add("Email:Provider");
            return;
        }

        if (provider == EmailProvider.Smtp)
        {
            AddMissingSmtpKeys(configuration, missingKeys);
            return;
        }

        if (provider == EmailProvider.BrevoApi)
        {
            AddMissingBrevoApiKeys(configuration, missingKeys);
        }
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
        AddIfMissing(configuration, $"{section}:TenantId", missingKeys);

        var tenantId = configuration[$"{section}:TenantId"];
        if (!string.IsNullOrWhiteSpace(tenantId) &&
            !tenantId.Equals("common", StringComparison.OrdinalIgnoreCase) &&
            !tenantId.Equals("organizations", StringComparison.OrdinalIgnoreCase) &&
            !tenantId.Equals("consumers", StringComparison.OrdinalIgnoreCase) &&
            !Guid.TryParse(tenantId, out _))
        {
            throw new InvalidOperationException(
                $"DumpTether configuration value '{section}:TenantId' must be a tenant GUID, " +
                "'common', 'organizations', or 'consumers'.");
        }
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

    private static EmailProvider GetEmailProvider(IConfiguration configuration)
    {
        var value = configuration["Email:Provider"];

        if (string.IsNullOrWhiteSpace(value))
        {
            return EmailProvider.None;
        }

        if (Enum.TryParse<EmailProvider>(value, ignoreCase: true, out var provider) &&
            Enum.IsDefined(provider))
        {
            return provider;
        }

        throw new InvalidOperationException(
            "DumpTether configuration value 'Email:Provider' must be one of " +
            "None, Smtp, or BrevoApi.");
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

    private static void ValidateDesktopRuntime(IConfiguration configuration)
    {
        RequireValue(configuration, "Database:Provider", "Sqlite");
        RequireBoolean(configuration, "Auth:RequireAuthentication", expected: true);
        RequireBoolean(configuration, "Auth:AllowGuestSessions", expected: false);
        RequireBoolean(configuration, "Auth:EnableDevelopmentLogin", expected: false);
        RequireBoolean(configuration, "Auth:EnableLocalDesktopLogin", expected: true);
        RequireValue(configuration, "Auth:SignupMode", "Closed");
        RequireBoolean(configuration, "EmailConfirmation:Enabled", expected: false);
        RequireBoolean(configuration, "PasswordRecovery:Enabled", expected: false);
        RequireBoolean(configuration, "AccountDeletion:Enabled", expected: false);
        RequireBoolean(configuration, "Notifications:Enabled", expected: false);
        RequireBoolean(configuration, "Mfa:Email:Enabled", expected: false);
        RequireBoolean(configuration, "OAuth:Microsoft:Enabled", expected: false);

        var urls = configuration["Urls"];
        if (string.IsNullOrWhiteSpace(urls))
        {
            throw new InvalidOperationException(
                "Desktop runtime requires a loopback-only 'Urls' setting.");
        }

        foreach (var value in urls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttp ||
                !uri.IsLoopback)
            {
                throw new InvalidOperationException(
                    "Desktop runtime 'Urls' must contain only HTTP loopback addresses.");
            }
        }

        var allowedOrigins = CorsConfiguration.GetAllowedOrigins(configuration);
        if (allowedOrigins.Length != 1 ||
            (!allowedOrigins[0].Equals("http://tauri.localhost", StringComparison.OrdinalIgnoreCase) &&
             !allowedOrigins[0].Equals("http://localhost:5173", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "Desktop runtime CORS must allow exactly the packaged Tauri origin " +
                "or the local Tauri development origin.");
        }

        var bootstrapToken = configuration["Desktop:BootstrapToken"];
        if (!string.IsNullOrWhiteSpace(bootstrapToken) &&
            (bootstrapToken.Length != 64 || bootstrapToken.Any(character => !Uri.IsHexDigit(character))))
        {
            throw new InvalidOperationException(
                "Desktop runtime bootstrap token must contain exactly 64 hexadecimal characters.");
        }
    }

    private static void RequireValue(
        IConfiguration configuration,
        string key,
        string expected)
    {
        if (!string.Equals(configuration[key], expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Desktop runtime setting '{key}' must be '{expected}'.");
        }
    }

    private static void RequireBoolean(
        IConfiguration configuration,
        string key,
        bool expected)
    {
        if (GetBoolean(configuration, key) != expected)
        {
            throw new InvalidOperationException(
                $"Desktop runtime setting '{key}' must be '{expected.ToString().ToLowerInvariant()}'.");
        }
    }
}
