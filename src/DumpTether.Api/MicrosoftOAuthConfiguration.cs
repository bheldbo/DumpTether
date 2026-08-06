using DumpTether.App.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.Validators;

namespace DumpTether.Api;

internal static class MicrosoftOAuthConfiguration
{
    public static void Configure(
        OpenIdConnectOptions options,
        OAuthProviderOptions provider,
        bool isDevelopment)
    {
        var authority =
            $"https://login.microsoftonline.com/{provider.TenantId}/v2.0";
        options.SignInScheme = AuthSchemes.ExternalCookie;
        options.Authority = authority;
        options.ClientId = provider.ClientId;
        options.ClientSecret = provider.ClientSecret;
        options.CallbackPath = "/api/auth/oauth/microsoft/callback";
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.UsePkce = true;
        options.SaveTokens = false;
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = !isDevelopment;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "name"
        };

        if (provider.TenantId.Equals("common", StringComparison.OrdinalIgnoreCase))
        {
            options.TokenValidationParameters.IssuerValidator =
                AadIssuerValidator.GetAadIssuerValidator(authority).Validate;
        }

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Events = new OpenIdConnectEvents
        {
            OnRemoteFailure = HandleRemoteFailure
        };
    }

    private static Task HandleRemoteFailure(RemoteFailureContext context)
    {
        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("DumpTether.Api.Auth.Microsoft");
        logger.LogWarning(
            "Microsoft sign-in failed with {FailureType}.",
            context.Failure?.GetType().Name ?? "UnknownFailure");

        string? returnUrl = null;
        context.Properties?.Items.TryGetValue(
            AuthSchemes.OAuthReturnUrlItem,
            out returnUrl);
        if (string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith('/'))
        {
            returnUrl = "/";
        }

        context.Response.Redirect(QueryHelpers.AddQueryString(
            returnUrl,
            "oauthError",
            "external_login_failed"));
        context.HandleResponse();
        return Task.CompletedTask;
    }
}
