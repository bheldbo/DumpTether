using DumpTether.App;
using DumpTether.App.Auth;
using DumpTether.App.Email;
using DumpTether.Api;
using DumpTether.App.LiveUpdates;
using DumpTether.Data;
using DumpTether.App.Usage;
using DumpTether.App.Workspaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<EmailConfirmationOptions>(builder.Configuration.GetSection("EmailConfirmation"));
builder.Services.Configure<OAuthOptions>(builder.Configuration.GetSection("OAuth"));
builder.Services.Configure<UsageOptions>(builder.Configuration.GetSection("Usage"));
builder.Services.PostConfigure<AuthOptions>(options =>
{
    if (!builder.Environment.IsDevelopment())
    {
        options.RequireAuthentication = true;
        options.EnableDevelopmentLogin = false;
    }
});
RuntimeConfigurationValidator.Validate(
    builder.Configuration,
    builder.Environment.IsDevelopment());
builder.Services.AddDumpTetherApplication();
builder.Services.RemoveAll<IEmailSender>();
builder.Services.RemoveAll<ILiveUpdatePublisher>();
builder.Services.AddHttpClient<IEmailSender, BrevoEmailSender>();
builder.Services.AddSingleton<ILiveUpdatePublisher, SignalRLiveUpdatePublisher>();
builder.Services.AddDumpTetherData(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuthTokenAccessor, CurrentAuthTokenAccessor>();
builder.Services.AddScoped<ICurrentWorkspaceSelection, CurrentWorkspaceSelection>();
ConfigureAuthentication(builder.Services, builder.Configuration, builder.Environment);
builder.Services.AddSingleton<IAuthorizationHandler, SessionRequiredAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, WorkspaceWriteAuthorizationHandler>();
builder.Services.AddAuthorization(options =>
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
builder.Services.AddRateLimiter(options =>
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
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddHostedService<SessionCleanupHostedService>();

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
    var databaseProvider = dbContext.Database.ProviderName;

    if (string.Equals(
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

app.UseRateLimiter();
app.UseMiddleware<SessionCsrfProtectionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<LiveUpdateHub>("/api/live")
    .RequireAuthorization(AuthPolicies.SessionRequired);

app.Run();

static string GetRateLimitKey(HttpContext context)
{
    var authorization = context.Request.Headers.Authorization.FirstOrDefault();

    if (!string.IsNullOrWhiteSpace(authorization))
    {
        return authorization;
    }

    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown-client";
}

static void ConfigureAuthentication(
    IServiceCollection services,
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
}

public partial class Program;
