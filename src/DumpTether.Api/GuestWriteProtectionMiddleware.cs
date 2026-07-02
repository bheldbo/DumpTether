using System.Security.Claims;
using DumpTether.Domain;

namespace DumpTether.Api;

internal sealed class GuestWriteProtectionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GuestWriteProtectionMiddleware> _logger;

    public GuestWriteProtectionMiddleware(
        RequestDelegate next,
        ILogger<GuestWriteProtectionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsProtectedWrite(context) ||
            !IsGuestSession(context.User) ||
            IsAllowedGuestWrite(context.Request.Path))
        {
            await _next(context);
            return;
        }

        _logger.LogWarning(
            "Auth audit event guest_write_rejected. Path: {Path}. Method: {Method}.",
            context.Request.Path.Value,
            context.Request.Method);
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Sign in with an account to save changes on this server."
        });
    }

    private static bool IsProtectedWrite(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return HttpMethods.IsPost(context.Request.Method) ||
            HttpMethods.IsPut(context.Request.Method) ||
            HttpMethods.IsPatch(context.Request.Method) ||
            HttpMethods.IsDelete(context.Request.Method);
    }

    private static bool IsGuestSession(ClaimsPrincipal user) =>
        string.Equals(
            user.FindFirstValue("dumptether:session_type"),
            UserSessionType.Guest.ToString(),
            StringComparison.Ordinal);

    private static bool IsAllowedGuestWrite(PathString path) =>
        path.StartsWithSegments("/api/auth", StringComparison.OrdinalIgnoreCase);
}
