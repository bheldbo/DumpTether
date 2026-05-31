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
    private const string TestConnectionString =
        "Host=localhost;Database=dumptether_tests;Username=dumptether;Password=dumptether";

    private SqliteConnection? _connection;
    private readonly string? _previousConnectionString;
    private readonly bool _requireAuthentication;
    private readonly bool _enableDevelopmentLogin;

    public DumpTetherApiFactory(
        bool requireAuthentication = false,
        bool enableDevelopmentLogin = false)
    {
        _requireAuthentication = requireAuthentication;
        _enableDevelopmentLogin = enableDevelopmentLogin;
        _previousConnectionString = Environment.GetEnvironmentVariable(ConnectionStringKey);
        Environment.SetEnvironmentVariable(ConnectionStringKey, TestConnectionString);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
        });

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DumpTether"] =
                    "Host=localhost;Database=dumptether_tests;Username=dumptether;Password=dumptether",
                ["Auth:RequireAuthentication"] = _requireAuthentication.ToString(),
                ["Auth:EnableDevelopmentLogin"] = _enableDevelopmentLogin.ToString()
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<DumpTetherDbContext>>();

            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            services.AddDbContext<DumpTetherDbContext>(options =>
                options.UseSqlite(_connection));

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
        }
    }
}
