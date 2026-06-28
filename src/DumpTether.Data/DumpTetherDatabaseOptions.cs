using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;

namespace DumpTether.Data;

public static class DumpTetherDatabaseOptions
{
    public const string PostgresProvider = "Postgres";
    public const string SqliteProvider = "Sqlite";

    public static string GetProvider(IConfiguration configuration)
    {
        var configuredProvider = configuration["Database:Provider"];

        if (string.IsNullOrWhiteSpace(configuredProvider))
        {
            return PostgresProvider;
        }

        return NormalizeProvider(configuredProvider);
    }

    public static bool IsPostgres(string provider) =>
        string.Equals(
            NormalizeProvider(provider),
            PostgresProvider,
            StringComparison.Ordinal);

    public static bool IsSqlite(string provider) =>
        string.Equals(
            NormalizeProvider(provider),
            SqliteProvider,
            StringComparison.Ordinal);

    public static string GetSqliteConnectionString(IConfiguration configuration)
    {
        var configuredPath = configuration["Database:Sqlite:Path"];

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return BuildSqliteConnectionString(ExpandLocalPath(configuredPath));
        }

        var connectionString = configuration.GetConnectionString("DumpTether");

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            if (!IsLikelySqliteConnectionString(connectionString))
            {
                throw new InvalidOperationException(
                    "Database:Provider is Sqlite, but ConnectionStrings:DumpTether does not look like a SQLite connection string. " +
                    "Use 'Data Source=...' or set Database:Sqlite:Path.");
            }

            return connectionString;
        }

        return BuildSqliteConnectionString(GetDefaultSqliteDatabasePath());
    }

    private static string BuildSqliteConnectionString(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return $"Data Source={databasePath}";
    }

    private static bool IsLikelySqliteConnectionString(string connectionString) =>
        connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) ||
        connectionString.Contains("Filename=", StringComparison.OrdinalIgnoreCase);

    public static string GetSqliteDatabasePath(IConfiguration configuration)
    {
        var configuredPath = configuration["Database:Sqlite:Path"];

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return ExpandLocalPath(configuredPath);
        }

        return GetDefaultSqliteDatabasePath();
    }

    public static string GetDefaultSqliteDatabasePath()
    {
        var appDataRoot = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
            : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        if (string.IsNullOrWhiteSpace(appDataRoot))
        {
            appDataRoot = AppContext.BaseDirectory;
        }

        return Path.Combine(appDataRoot, "DumpTether", "dumptether.db");
    }

    public static string NormalizeProvider(string provider)
    {
        var normalized = provider.Trim().ToLowerInvariant();

        return normalized switch
        {
            "postgres" or "postgresql" or "npgsql" => PostgresProvider,
            "sqlite" or "sqlite3" => SqliteProvider,
            _ => throw new InvalidOperationException(
                $"Unsupported Database:Provider '{provider}'. Use '{PostgresProvider}' or '{SqliteProvider}'.")
        };
    }

    private static string ExpandLocalPath(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path);

        if (expanded == "~")
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        if (expanded.StartsWith($"~{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            expanded.StartsWith($"~{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, expanded[2..]);
        }

        return expanded;
    }
}
