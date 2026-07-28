using System.Security.Cryptography;
using System.Text;

namespace DumpTether.Api;

internal sealed class DesktopBootstrapTokenMiddleware
{
    public const string HeaderName = "X-DumpTether-Desktop-Bootstrap";

    private readonly RequestDelegate _next;
    private readonly byte[]? _expectedToken;

    public DesktopBootstrapTokenMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _next = next;

        if (environment.IsEnvironment("Desktop"))
        {
            var configuredToken = configuration["Desktop:BootstrapToken"];
            if (!string.IsNullOrWhiteSpace(configuredToken))
            {
                _expectedToken = Encoding.UTF8.GetBytes(configuredToken);
            }
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_expectedToken is null ||
            HttpMethods.IsOptions(context.Request.Method) ||
            context.WebSockets.IsWebSocketRequest)
        {
            await _next(context);
            return;
        }

        var providedToken = context.Request.Headers[HeaderName].ToString();
        var providedBytes = Encoding.UTF8.GetBytes(providedToken);
        var matches = providedBytes.Length == _expectedToken.Length &&
            CryptographicOperations.FixedTimeEquals(providedBytes, _expectedToken);

        if (!matches)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Desktop runtime handshake failed."
            });
            return;
        }

        await _next(context);
    }
}
