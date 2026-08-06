using Microsoft.Extensions.Configuration;

namespace DumpTether.Admin;

internal static class AdminConfiguration
{
    public static IConfiguration Build()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (string.IsNullOrWhiteSpace(environment))
        {
            environment = "Development";
        }

        var configurationRoot = FindConfigurationRoot();
        return new ConfigurationBuilder()
            .SetBasePath(configurationRoot)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();
    }

    private static string FindConfigurationRoot()
    {
        if (File.Exists(Path.Combine(Environment.CurrentDirectory, "appsettings.json")))
        {
            return Environment.CurrentDirectory;
        }

        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var solutionPath = Path.Combine(directory.FullName, "DumpTether.sln");
            var apiPath = Path.Combine(directory.FullName, "src", "DumpTether.Api");
            if (File.Exists(solutionPath) && File.Exists(Path.Combine(apiPath, "appsettings.json")))
            {
                return apiPath;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate DumpTether API configuration. Run from the repository or the API container.");
    }
}
