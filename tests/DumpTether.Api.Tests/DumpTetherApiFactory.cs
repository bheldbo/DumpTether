using DumpTether.App.LiveUpdates;
using DumpTether.App.Auth;
using DumpTether.App.Sync;
using DumpTether.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace DumpTether.Api.Tests;

internal sealed class DumpTetherApiFactory : WebApplicationFactory<Program>
{
    private const string ConnectionStringKey = "ConnectionStrings__DumpTether";
    private const string ApplyMigrationsOnStartupKey = "Database__ApplyMigrationsOnStartup";
    private const string TestConnectionString =
        "Host=localhost;Database=dumptether_tests;Username=dumptether;Password=dumptether";

    private SqliteConnection? _connection;
    private readonly string? _previousConnectionString;
    private readonly string? _previousApplyMigrationsOnStartup;
    private readonly bool _requireAuthentication;
    private readonly bool _enableDevelopmentLogin;
    private readonly int _maxActiveTasksPerWorkspace;
    private readonly int _maxTotalTasksPerWorkspace;
    private readonly string? _environmentName;
    private readonly IReadOnlyDictionary<string, string?> _extraConfiguration;
    private readonly ICloudSyncClient? _cloudSyncClient;
    private readonly ILiveUpdatePublisher? _liveUpdatePublisher;

    public DumpTetherApiFactory(
        bool requireAuthentication = false,
        bool enableDevelopmentLogin = false,
        string? environmentName = null,
        int maxActiveTasksPerWorkspace = 1000,
        int maxTotalTasksPerWorkspace = 5000,
        IReadOnlyDictionary<string, string?>? extraConfiguration = null,
        ICloudSyncClient? cloudSyncClient = null,
        ILiveUpdatePublisher? liveUpdatePublisher = null)
    {
        _requireAuthentication = requireAuthentication;
        _enableDevelopmentLogin = enableDevelopmentLogin;
        _environmentName = environmentName;
        _maxActiveTasksPerWorkspace = maxActiveTasksPerWorkspace;
        _maxTotalTasksPerWorkspace = maxTotalTasksPerWorkspace;
        _extraConfiguration = extraConfiguration ?? new Dictionary<string, string?>();
        _cloudSyncClient = cloudSyncClient;
        _liveUpdatePublisher = liveUpdatePublisher;
        _previousConnectionString = Environment.GetEnvironmentVariable(ConnectionStringKey);
        _previousApplyMigrationsOnStartup =
            Environment.GetEnvironmentVariable(ApplyMigrationsOnStartupKey);
        Environment.SetEnvironmentVariable(
            ConnectionStringKey,
            string.Equals(environmentName, "Desktop", StringComparison.OrdinalIgnoreCase)
                ? "Data Source=:memory:"
                : TestConnectionString);
        Environment.SetEnvironmentVariable(ApplyMigrationsOnStartupKey, "false");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(string.IsNullOrWhiteSpace(_environmentName)
            ? "Development"
            : _environmentName);

        builder.ConfigureLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
        });

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var connectionString = string.Equals(
                _environmentName,
                "Desktop",
                StringComparison.OrdinalIgnoreCase)
                    ? "Data Source=:memory:"
                    : "Host=localhost;Database=dumptether_tests;Username=dumptether;Password=dumptether";
            var configuration = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DumpTether"] = connectionString,
                ["Auth:RequireAuthentication"] = _requireAuthentication.ToString(),
                ["Auth:EnableDevelopmentLogin"] = _enableDevelopmentLogin.ToString(),
                ["Database:ApplyMigrationsOnStartup"] = "false",
                ["Usage:MaxActiveTasksPerWorkspace"] = _maxActiveTasksPerWorkspace.ToString(),
                ["Usage:MaxTotalTasksPerWorkspace"] = _maxTotalTasksPerWorkspace.ToString()
            };

            foreach (var item in _extraConfiguration)
            {
                configuration[item.Key] = item.Value;
            }

            configurationBuilder.AddInMemoryCollection(configuration);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<DumpTetherDbContext>>();

            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            services.AddDbContext<DumpTetherDbContext>(options =>
                options.UseSqlite(_connection));

            if (_liveUpdatePublisher is not null)
            {
                services.RemoveAll<ILiveUpdatePublisher>();
                services.AddSingleton(_liveUpdatePublisher);
            }

            if (_cloudSyncClient is not null)
            {
                services.RemoveAll<ICloudSyncClient>();
                services.AddSingleton(_cloudSyncClient);
            }

            services.PostConfigure<AuthOptions>(options =>
            {
                options.RequireAuthentication = _requireAuthentication;
                options.EnableDevelopmentLogin = _enableDevelopmentLogin;
            });

            using var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
            dbContext.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection?.Dispose();
            Environment.SetEnvironmentVariable(ConnectionStringKey, _previousConnectionString);
            Environment.SetEnvironmentVariable(
                ApplyMigrationsOnStartupKey,
                _previousApplyMigrationsOnStartup);
        }
    }
}
