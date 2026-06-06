using System.Security.Claims;
using System.Text.Encodings.Web;
using DumpTether.App.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

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
            return AuthenticateResult.NoResult();
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, session.UserId.ToString()),
            new Claim(ClaimTypes.Email, session.Email),
            new Claim(ClaimTypes.Name, session.DisplayName)
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);

        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}
