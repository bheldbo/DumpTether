using DumpTether.App;
using DumpTether.App.Auth;
using DumpTether.Api;
using DumpTether.Data;
using DumpTether.App.Usage;
using DumpTether.App.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection("Auth"));
builder.Services.Configure<UsageOptions>(builder.Configuration.GetSection("Usage"));
builder.Services.PostConfigure<AuthOptions>(options =>
{
    if (!builder.Environment.IsDevelopment())
    {
        options.RequireAuthentication = true;
        options.EnableDevelopmentLogin = false;
    }
});
builder.Services.AddDumpTetherApplication();
builder.Services.AddDumpTetherData(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuthTokenAccessor, CurrentAuthTokenAccessor>();
builder.Services.AddScoped<ICurrentWorkspaceSelection, CurrentWorkspaceSelection>();
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.PermitLimit = 20;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("task-writes", limiterOptions =>
    {
        limiterOptions.PermitLimit = 120;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
});
builder.Services.AddControllers();

var app = builder.Build();

if (builder.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<DumpTetherDbContext>();
    await dbContext.Database.MigrateAsync();
}

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
app.MapControllers();

app.Run();

public partial class Program;
