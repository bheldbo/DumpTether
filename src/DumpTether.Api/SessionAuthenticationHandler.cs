using System.Security.Claims;
using System.Text.Encodings.Web;
using DumpTether.App.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace DumpTether.Api;

internal sealed class SessionAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ICurrentUserSessionProvider _currentUserSessionProvider;

    public SessionAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ICurrentUserSessionProvider currentUserSessionProvider)
        : base(options, logger, encoder)
    {
        _currentUserSessionProvider = currentUserSessionProvider;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var session = await _currentUserSessionProvider.GetCurrentAsync(Context.RequestAborted);

        if (session is null)
        {
            var tokenSource = GetTokenSource();
            if (tokenSource is not null)
            {
                Logger.LogWarning(
                    "Session authentication failed for {Path}. Token source: {TokenSource}.",
                    Request.Path.Value,
                    tokenSource);
            }

            return AuthenticateResult.NoResult();
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, session.UserId.ToString()),
            new Claim("dumptether:session_id", session.SessionId.ToString()),
            new Claim("dumptether:session_type", session.SessionType.ToString()),
            new Claim(ClaimTypes.Email, session.Email),
            new Claim(ClaimTypes.Name, session.DisplayName)
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);

        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    private string? GetTokenSource()
    {
        var authorization = Request.Headers[HeaderNames.Authorization].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(authorization) &&
            authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return "bearer";
        }

        if (Request.Path.StartsWithSegments("/api/live", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(Request.Query["access_token"].FirstOrDefault()))
        {
            return "query";
        }

        return Request.Cookies.ContainsKey(SessionCsrfProtectionMiddleware.SessionCookieName)
            ? "cookie"
            : null;
    }
}
