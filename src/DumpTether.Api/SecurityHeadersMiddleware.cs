namespace DumpTether.Api;

internal sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;

    public SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            headers.TryAdd("X-Content-Type-Options", "nosniff");
            headers.TryAdd("X-Frame-Options", "DENY");
            headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
            headers.TryAdd("X-Permitted-Cross-Domain-Policies", "none");

            if (!_environment.IsDevelopment() && context.Request.IsHttps)
            {
                headers.TryAdd("Strict-Transport-Security", "max-age=31536000");
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }
}
