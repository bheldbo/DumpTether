using System.Security.Cryptography;
using System.Text;
using Microsoft.Net.Http.Headers;

namespace DumpTether.Api;

internal sealed class SessionCsrfProtectionMiddleware
{
    public const string SessionCookieName = "DumpTether.Session";
    public const string CsrfCookieName = "DumpTether.Csrf";
    public const string CsrfHeaderName = "X-DumpTether-CSRF";

    private readonly RequestDelegate _next;

    public SessionCsrfProtectionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsSafeMethod(context.Request.Method) ||
            !HasSessionCookie(context) ||
            HasBearerOrQueryToken(context))
        {
            await _next(context);
            return;
        }

        var csrfCookie = context.Request.Cookies[CsrfCookieName];
        var csrfHeader = context.Request.Headers[CsrfHeaderName].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(csrfCookie) ||
            string.IsNullOrWhiteSpace(csrfHeader) ||
            !TokensMatch(csrfCookie, csrfHeader))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "CSRF token is required." });
            return;
        }

        await _next(context);
    }

    private static bool IsSafeMethod(string method) =>
        HttpMethods.IsGet(method) ||
        HttpMethods.IsHead(method) ||
        HttpMethods.IsOptions(method) ||
        HttpMethods.IsTrace(method);

    private static bool HasSessionCookie(HttpContext context) =>
        context.Request.Cookies.ContainsKey(SessionCookieName);

    private static bool HasBearerOrQueryToken(HttpContext context)
    {
        var authorization = context.Request.Headers[HeaderNames.Authorization].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(authorization) &&
            authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(context.Request.Query["access_token"].FirstOrDefault());
    }

    private static bool TokensMatch(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);

        return leftBytes.Length == rightBytes.Length &&
            CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
