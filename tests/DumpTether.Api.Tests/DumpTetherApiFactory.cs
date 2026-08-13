using DumpTether.App.LiveUpdates;
using DumpTether.App.Auth;
using DumpTether.App.Email;
using DumpTether.App.Sync;
using DumpTether.App.Tasks;
using DumpTether.Data;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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
    private readonly IEmailSender? _emailSender;
    private readonly IClock? _clock;

    public DumpTetherApiFactory(
        bool requireAuthentication = false,
        bool enableDevelopmentLogin = false,
        string? environmentName = null,
        int maxActiveTasksPerWorkspace = 1000,
        int maxTotalTasksPerWorkspace = 5000,
        IReadOnlyDictionary<string, string?>? extraConfiguration = null,
        ICloudSyncClient? cloudSyncClient = null,
        ILiveUpdatePublisher? liveUpdatePublisher = null,
        IEmailSender? emailSender = null,
        IClock? clock = null)
    {
        _requireAuthentication = requireAuthentication;
        _enableDevelopmentLogin = enableDevelopmentLogin;
        _environmentName = environmentName;
        _maxActiveTasksPerWorkspace = maxActiveTasksPerWorkspace;
        _maxTotalTasksPerWorkspace = maxTotalTasksPerWorkspace;
        _extraConfiguration = extraConfiguration ?? new Dictionary<string, string?>();
        _cloudSyncClient = cloudSyncClient;
        _liveUpdatePublisher = liveUpdatePublisher;
        _emailSender = emailSender;
        _clock = clock;
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
                ["Desktop:CloudLiveRelayEnabled"] = "false",
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
            var cloudRelayRegistration = services.FirstOrDefault(descriptor =>
                descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType?.Name ==
                    "DesktopCloudLiveUpdateRelayHostedService");
            if (cloudRelayRegistration is not null)
            {
                services.Remove(cloudRelayRegistration);
            }

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

            if (_emailSender is not null)
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton(_emailSender);
            }

            if (_clock is not null)
            {
                services.RemoveAll<IClock>();
                services.AddSingleton(_clock);
            }

            services.RemoveAll<ICloudSessionProtector>();
            services.AddSingleton<ICloudSessionProtector, TestCloudSessionProtector>();

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

    private sealed class TestCloudSessionProtector : ICloudSessionProtector
    {
        private const string Prefix = "protected:";

        public string Protect(string sessionToken)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Prefix}{sessionToken}"));
        }

        public string Unprotect(string protectedSessionToken)
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(protectedSessionToken));
            return decoded.StartsWith(Prefix, StringComparison.Ordinal)
                ? decoded[Prefix.Length..]
                : throw new InvalidOperationException("Invalid protected test token.");
        }
    }
}
