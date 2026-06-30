using System.Threading.RateLimiting;
using DumpTether.App;
using DumpTether.App.Auth;
using DumpTether.App.Email;
using DumpTether.App.LiveUpdates;
using DumpTether.App.Usage;
using DumpTether.App.Workspaces;
using DumpTether.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        services.Configure<EmailOptions>(configuration.GetSection("Email"));
        services.Configure<EmailConfirmationOptions>(configuration.GetSection("EmailConfirmation"));
        services.Configure<OAuthOptions>(configuration.GetSection("OAuth"));
        services.Configure<UsageOptions>(configuration.GetSection("Usage"));
        services.PostConfigure<AuthOptions>(options =>
        {
            if (!environment.IsDevelopment())
            {
                options.RequireAuthentication = true;
                options.EnableDevelopmentLogin = false;
            }
        });

        services.AddDumpTetherApplication();
        services.RemoveAll<IEmailSender>();
        services.RemoveAll<ILiveUpdatePublisher>();
        services.AddHttpClient<IEmailSender, BrevoEmailSender>();
        services.AddSingleton<ILiveUpdatePublisher, SignalRLiveUpdatePublisher>();
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
        services.AddHostedService<SessionCleanupHostedService>();

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
                await dbContext.Database.EnsureCreatedAsync();
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "DumpTether could not create the local SQLite database on startup. " +
                    "Check Database:Sqlite:Path permissions or remove the local database file and retry.",
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

        app.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            service = "DumpTether.Api"
        }));

        app.UseCors(runtimeSetup.CorsPolicyName);
        app.UseRateLimiter();
        app.UseMiddleware<SessionCsrfProtectionMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHub<LiveUpdateHub>("/api/live")
            .RequireAuthorization(AuthPolicies.SessionRequired);

        return app;
    }

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

        if (oauthOptions.Google.Enabled)
        {
            authenticationBuilder.AddGoogle("google", options =>
            {
                options.SignInScheme = AuthSchemes.ExternalCookie;
                options.ClientId = oauthOptions.Google.ClientId;
                options.ClientSecret = oauthOptions.Google.ClientSecret;
                options.CallbackPath = "/api/auth/oauth/google/callback";
                options.Scope.Add("email");
            });
        }

        if (oauthOptions.Microsoft.Enabled)
        {
            authenticationBuilder.AddMicrosoftAccount("microsoft", options =>
            {
                options.SignInScheme = AuthSchemes.ExternalCookie;
                options.ClientId = oauthOptions.Microsoft.ClientId;
                options.ClientSecret = oauthOptions.Microsoft.ClientSecret;
                options.CallbackPath = "/api/auth/oauth/microsoft/callback";
                options.Scope.Add("email");
            });
        }

        if (oauthOptions.Facebook.Enabled)
        {
            authenticationBuilder.AddFacebook("facebook", options =>
            {
                options.SignInScheme = AuthSchemes.ExternalCookie;
                options.AppId = oauthOptions.Facebook.ClientId;
                options.AppSecret = oauthOptions.Facebook.ClientSecret;
                options.CallbackPath = "/api/auth/oauth/facebook/callback";
                options.Scope.Add("email");
                options.Fields.Add("email");
                options.Fields.Add("name");
            });
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
            options.AddPolicy("auth", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetRateLimitKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
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
            return authorization;
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown-client";
    }
}
