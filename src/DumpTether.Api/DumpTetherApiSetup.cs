using System.Threading.RateLimiting;
using DumpTether.App;
using DumpTether.App.Auth;
using DumpTether.App.Email;
using DumpTether.App.LiveUpdates;
using DumpTether.App.Notifications;
using DumpTether.App.Sync;
using DumpTether.App.Usage;
using DumpTether.App.Workspaces;
using DumpTether.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DumpTether.Api;

internal static class DumpTetherApiSetup
{
    public static IServiceCollection AddDumpTetherApiRuntime(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        DumpTetherRuntimeSetup runtimeSetup)
    {
        services.Configure<AuthOptions>(configuration.GetSection("Auth"));
        services.Configure<EmailConfirmationOptions>(configuration.GetSection("EmailConfirmation"));
        services.Configure<OAuthOptions>(configuration.GetSection("OAuth"));
        services.Configure<LegalOptions>(configuration.GetSection("Legal"));
        services.Configure<UsageOptions>(configuration.GetSection("Usage"));
        services.Configure<PasswordRecoveryOptions>(configuration.GetSection("PasswordRecovery"));
        services.Configure<AccountDeletionOptions>(configuration.GetSection("AccountDeletion"));
        services.Configure<NotificationOptions>(configuration.GetSection("Notifications"));
        services.PostConfigure<AuthOptions>(options =>
        {
            if (!environment.IsDevelopment())
            {
                options.RequireAuthentication = true;
                options.EnableDevelopmentLogin = false;
            }
        });

        services.AddDumpTetherApplication();
        services.AddDumpTetherTransactionalEmail(configuration);
        services.RemoveAll<ILiveUpdatePublisher>();
        services.RemoveAll<ICloudSyncClient>();
        services.RemoveAll<ICloudSessionProtector>();
        services.AddHttpClient<ICloudSyncClient, HttpCloudSyncClient>();
        services.AddSingleton<ILiveUpdatePublisher, SignalRLiveUpdatePublisher>();
        services.AddSingleton<ICloudSessionProtector, DataProtectionCloudSessionProtector>();
        services.AddDumpTetherData(configuration);
        services.AddHttpContextAccessor();
        services.AddScoped<IAuthTokenAccessor, CurrentAuthTokenAccessor>();
        services.AddScoped<ICurrentWorkspaceSelection, CurrentWorkspaceSelection>();

        services.AddDumpTetherDataProtection(configuration, environment);
        services.AddDumpTetherAuthentication(configuration, environment);
        services.AddDumpTetherCors(runtimeSetup);
        services.AddDumpTetherAuthorizationPolicies();
        services.AddDumpTetherRateLimiting();
        services.AddControllers();
        services.AddSignalR();
        services.AddSingleton<DatabaseReadinessHealthCheck>();
        services
            .AddHealthChecks()
            .AddCheck<DatabaseReadinessHealthCheck>(
                "database",
                tags: ["ready"]);
        services.AddHostedService<SessionCleanupHostedService>();
        services.AddHostedService<AccountDeletionHostedService>();
        services.AddHostedService<NotificationDeliveryHostedService>();
        if (environment.IsEnvironment("Desktop") &&
            configuration.GetValue<bool>("Desktop:CloudLiveRelayEnabled"))
        {
            services.AddHostedService<DesktopCloudLiveUpdateRelayHostedService>();
        }

        if (runtimeSetup.TrustForwardedHeaders)
        {
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders =
                    ForwardedHeaders.XForwardedFor |
                    ForwardedHeaders.XForwardedProto;

                // Caddy is the only production network path to this container,
                // but its private Docker address is allocated dynamically.
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
            });
        }

        return services;
    }

    public static async Task ApplyDumpTetherDatabaseStartupAsync(
        this WebApplication app,
        DumpTetherRuntimeSetup runtimeSetup)
    {
        if (!runtimeSetup.ApplyMigrationsOnStartup)
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
        var databaseProvider = dbContext.Database.ProviderName;

        if (string.Equals(
                databaseProvider,
                "Microsoft.EntityFrameworkCore.Sqlite",
                StringComparison.Ordinal))
        {
            try
            {
                await dbContext.Database.MigrateAsync();
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "DumpTether could not apply SQLite migrations for the local database on startup. " +
                    "Check Database:Sqlite:Path permissions, run the DumpTether.Database maintenance tool, " +
                    "or remove the local database file and retry if this is disposable development data.",
                    exception);
            }
        }
        else if (string.Equals(
                     databaseProvider,
                     "Npgsql.EntityFrameworkCore.PostgreSQL",
                     StringComparison.Ordinal))
        {
            try
            {
                await dbContext.Database.MigrateAsync();
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "DumpTether could not apply EF Core migrations on startup. " +
                    "For local development, start PostgreSQL and run scripts/dev.ps1 -Target Migrate, " +
                    "or keep Database:ApplyMigrationsOnStartup enabled in Development. " +
                    "The database schema is probably older than the current code.",
                    exception);
            }
        }
    }

    public static WebApplication UseDumpTetherApiRuntime(
        this WebApplication app,
        DumpTetherRuntimeSetup runtimeSetup)
    {
        if (runtimeSetup.TrustForwardedHeaders)
        {
            app.UseForwardedHeaders();
        }

        app.UseMiddleware<SecurityHeadersMiddleware>();

        app.Use(async (context, next) =>
        {
            try
            {
                await next();
            }
            catch (UnauthorizedAccessException)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { error = "Authentication is required." });
            }
        });

        app.MapHealthChecks("/health", CreateHealthCheckOptions(_ => false))
            .RequireRateLimiting("health");
        app.MapHealthChecks("/health/live", CreateHealthCheckOptions(_ => false))
            .RequireRateLimiting("health");
        app.MapHealthChecks(
            "/health/ready",
            CreateHealthCheckOptions(check => check.Tags.Contains("ready")))
            .RequireRateLimiting("health");

        app.UseCors(runtimeSetup.CorsPolicyName);
        app.UseMiddleware<DesktopBootstrapTokenMiddleware>();
        app.UseRateLimiter();
        app.UseMiddleware<SessionCsrfProtectionMiddleware>();
        app.UseAuthentication();
        app.UseMiddleware<GuestWriteProtectionMiddleware>();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHub<LiveUpdateHub>("/api/live")
            .RequireAuthorization(AuthPolicies.SessionRequired);

        return app;
    }

    private static HealthCheckOptions CreateHealthCheckOptions(
        Func<HealthCheckRegistration, bool> predicate) =>
        new()
        {
            Predicate = predicate,
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new
                {
                    status = report.Status.ToString().ToLowerInvariant(),
                    service = "DumpTether.Api"
                });
            }
        };

    private static IServiceCollection AddDumpTetherDataProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var dataProtection = services
            .AddDataProtection()
            .SetApplicationName("DumpTether");

        var keysPath = configuration["DataProtection:KeysPath"];

        if (string.IsNullOrWhiteSpace(keysPath) &&
            string.Equals(environment.EnvironmentName, "Desktop", StringComparison.OrdinalIgnoreCase))
        {
            keysPath = GetDefaultDesktopDataProtectionKeysPath();
        }

        if (!string.IsNullOrWhiteSpace(keysPath))
        {
            var expandedPath = Environment.ExpandEnvironmentVariables(keysPath);
            Directory.CreateDirectory(expandedPath);
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(expandedPath));

            if (OperatingSystem.IsWindows())
            {
                dataProtection.ProtectKeysWithDpapi();
            }
        }

        return services;
    }

    private static string GetDefaultDesktopDataProtectionKeysPath()
    {
        var appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        if (string.IsNullOrWhiteSpace(appDataRoot))
        {
            appDataRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }

        if (string.IsNullOrWhiteSpace(appDataRoot))
        {
            appDataRoot = AppContext.BaseDirectory;
        }

        return Path.Combine(appDataRoot, "DumpTether", "keys");
    }

    private static IServiceCollection AddDumpTetherAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var oauthOptions = configuration.GetSection("OAuth").Get<OAuthOptions>() ?? new OAuthOptions();
        var authenticationBuilder = services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = AuthSchemes.Session;
                options.DefaultChallengeScheme = AuthSchemes.Session;
            })
            .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(
                AuthSchemes.Session,
                _ => { })
            .AddCookie(AuthSchemes.ExternalCookie, options =>
            {
                options.Cookie.Name = "DumpTether.External";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = environment.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
            });

        if (oauthOptions.Microsoft.Enabled)
        {
            authenticationBuilder.AddOpenIdConnect(
                "microsoft",
                options => MicrosoftOAuthConfiguration.Configure(
                    options,
                    oauthOptions.Microsoft,
                    environment.IsDevelopment()));
        }

        return services;
    }

    private static IServiceCollection AddDumpTetherCors(
        this IServiceCollection services,
        DumpTetherRuntimeSetup runtimeSetup)
    {
        services.AddCors(options =>
        {
            options.AddPolicy(runtimeSetup.CorsPolicyName, policy =>
            {
                if (runtimeSetup.CorsAllowedOrigins.Length == 0)
                {
                    return;
                }

                policy
                    .WithOrigins(runtimeSetup.CorsAllowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        return services;
    }

    private static IServiceCollection AddDumpTetherAuthorizationPolicies(
        this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, SessionRequiredAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, WorkspaceWriteAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthPolicies.SessionRequired, policy =>
            {
                policy.AddAuthenticationSchemes(AuthSchemes.Session);
                policy.Requirements.Add(new SessionRequiredRequirement());
            });
            options.AddPolicy(AuthPolicies.WorkspaceWriteRequired, policy =>
            {
                policy.AddAuthenticationSchemes(AuthSchemes.Session);
                policy.Requirements.Add(new SessionRequiredRequirement());
                policy.Requirements.Add(new WorkspaceWriteRequirement());
            });
        });

        return services;
    }

    private static IServiceCollection AddDumpTetherRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("health", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown-health-client",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
            options.AddPolicy("auth", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetRateLimitKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
            options.AddPolicy("account-recovery", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetIpRateLimitKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(15),
                        QueueLimit = 0
                    }));
            options.AddPolicy("task-writes", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetRateLimitKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 120,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
        });

        return services;
    }

    private static string GetRateLimitKey(HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization.FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(authorization))
        {
            var bytes = System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(authorization));
            return $"credential:{Convert.ToHexString(bytes)}";
        }

        return GetIpRateLimitKey(context);
    }

    private static string GetIpRateLimitKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown-client";
}
