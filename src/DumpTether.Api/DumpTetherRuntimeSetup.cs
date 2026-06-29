namespace DumpTether.Api;

public sealed record DumpTetherRuntimeSetup(
    string CorsPolicyName,
    string[] CorsAllowedOrigins,
    bool ApplyMigrationsOnStartup)
{
    public const string DefaultCorsPolicyName = "DumpTether.Cors";
}

internal static class DumpTetherRuntimeSetupReader
{
    public static DumpTetherRuntimeSetup Read(
        IConfiguration configuration,
        bool isDevelopment)
    {
        RuntimeConfigurationValidator.Validate(configuration, isDevelopment);

        return new DumpTetherRuntimeSetup(
            DumpTetherRuntimeSetup.DefaultCorsPolicyName,
            CorsConfiguration.GetAllowedOrigins(configuration),
            configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"));
    }
}
