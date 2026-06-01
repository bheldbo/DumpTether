using DumpTether.App.Auth;
using Microsoft.Net.Http.Headers;

namespace DumpTether.Api;

internal sealed class CurrentAuthTokenAccessor : IAuthTokenAccessor
{
    private const string SessionCookieName = "DumpTether.Session";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentAuthTokenAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string? SessionToken
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var authorization = httpContext?.Request.Headers[HeaderNames.Authorization].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(authorization) &&
                authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return authorization["Bearer ".Length..].Trim();
            }

            return httpContext?.Request.Cookies[SessionCookieName];
        }
    }
}
