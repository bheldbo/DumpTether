using DumpTether.App.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace DumpTether.Api;

internal sealed class SessionRequiredRequirement : IAuthorizationRequirement;

internal sealed class SessionRequiredAuthorizationHandler
    : AuthorizationHandler<SessionRequiredRequirement>
{
    private readonly IOptions<AuthOptions> _authOptions;

    public SessionRequiredAuthorizationHandler(IOptions<AuthOptions> authOptions)
    {
        _authOptions = authOptions;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SessionRequiredRequirement requirement)
    {
        if (!_authOptions.Value.RequireAuthentication ||
            context.User.Identity?.IsAuthenticated == true)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
