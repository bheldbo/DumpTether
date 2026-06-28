using Microsoft.Extensions.Configuration;

namespace DumpTether.Api;

internal static class CorsConfiguration
{
    public static string[] GetAllowedOrigins(IConfiguration configuration)
    {
        var values = new List<string>();
        var configuredValues = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>();
        var inlineValue = configuration["Cors:AllowedOrigins"];

        if (configuredValues is not null)
        {
            values.AddRange(configuredValues);
        }

        if (!string.IsNullOrWhiteSpace(inlineValue))
        {
            values.AddRange(inlineValue.Split(
                [',', ';'],
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        }

        return values
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static void ValidateAllowedOrigins(IConfiguration configuration)
    {
        foreach (var origin in GetAllowedOrigins(configuration))
        {
            if (origin == "*")
            {
                throw new InvalidOperationException(
                    "Cors:AllowedOrigins must list exact trusted origins. Do not use '*'.");
            }

            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                !string.IsNullOrWhiteSpace(uri.AbsolutePath.Trim('/')) ||
                !string.IsNullOrWhiteSpace(uri.Query))
            {
                throw new InvalidOperationException(
                    $"Cors:AllowedOrigins contains invalid origin '{origin}'. " +
                    "Use an exact origin such as 'https://dumptether.example.com' without a path or query string.");
            }
        }
    }
}
