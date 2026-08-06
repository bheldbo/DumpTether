using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using DumpTether.App.Auth;
using DumpTether.App.Email;
using DumpTether.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace DumpTether.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IEmailSender _emailSender;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly IOptions<AuthOptions> _authOptions;
    private readonly IOptions<EmailConfirmationOptions> _emailConfirmationOptions;
    private readonly IOptions<OAuthOptions> _oauthOptions;
    private readonly IOptions<LegalOptions> _legalOptions;

    public AuthController(
        IAuthService authService,
        IEmailSender emailSender,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        IOptions<AuthOptions> authOptions,
        IOptions<EmailConfirmationOptions> emailConfirmationOptions,
        IOptions<OAuthOptions> oauthOptions,
        IOptions<LegalOptions> legalOptions)
    {
        _authService = authService;
        _emailSender = emailSender;
        _environment = environment;
        _configuration = configuration;
        _authOptions = authOptions;
        _emailConfirmationOptions = emailConfirmationOptions;
        _oauthOptions = oauthOptions;
        _legalOptions = legalOptions;
    }

    [HttpGet("options")]
    public ActionResult<AuthClientOptionsResponse> GetOptions()
    {
        var options = _authOptions.Value;
        var legal = _legalOptions.Value;
        return Ok(new AuthClientOptionsResponse(
            options.RequireAuthentication,
            options.AllowGuestSessions,
            _environment.IsDevelopment() && options.EnableDevelopmentLogin,
            LocalDesktopLoginIsEnabled(),
            _emailConfirmationOptions.Value.Enabled,
            options.SignupMode,
            _oauthOptions.Value.EnabledProviders(),
            new LegalClientOptionsResponse(
                legal.RequireAcceptance,
                legal.TermsVersion,
                legal.PrivacyNoticeVersion,
                legal.OperatorName,
                legal.PrivacyContactEmail)));
    }

    [EnableRateLimiting("auth")]
    [HttpPost("register")]
    public async Task<ActionResult<RegisterUserResponse>> Register(
        RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _authService.RegisterAsync(request, cancellationToken);
            return Created("/api/auth/me", response);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (EmailDeliveryException exception)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { error = exception.Message });
        }
    }

    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    public async Task<ActionResult<LoginUserResponse>> Login(
        LoginUserRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _authService.LoginAsync(
                request,
                new AuthRequestMetadata(
                    Request.Headers.UserAgent.FirstOrDefault(),
                    HttpContext.Connection.RemoteIpAddress?.ToString()),
                cancellationToken);

            AppendSessionCookie(response);

            return Ok(response);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (ValidationException)
        {
            return Unauthorized(new { error = "Invalid email or password." });
        }
        catch (EmailConfirmationRequiredException)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { error = "Email confirmation is required." });
        }
    }

    [EnableRateLimiting("auth")]
    [HttpPost("desktop-cloud-login")]
    public async Task<ActionResult<LoginUserResponse>> DesktopCloudLogin(
        LoginUserRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _authService.DesktopCloudLoginAsync(
                request,
                new AuthRequestMetadata(
                    Request.Headers.UserAgent.FirstOrDefault(),
                    HttpContext.Connection.RemoteIpAddress?.ToString()),
                cancellationToken);

            return Ok(response);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (ValidationException)
        {
            return Unauthorized(new { error = "Invalid email or password." });
        }
        catch (EmailConfirmationRequiredException)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { error = "Email confirmation is required." });
        }
    }

    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(
        [FromQuery] string? token,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _authService.ConfirmEmailAsync(token ?? string.Empty, cancellationToken);
            return Content(
                EmailConfirmationPageRenderer.Success(
                    response.Email,
                    _emailConfirmationOptions.Value.PublicBaseUrl.TrimEnd('/')),
                "text/html");
        }
        catch (ValidationException)
        {
            return new ContentResult
            {
                Content = EmailConfirmationPageRenderer.Failure(
                    _emailConfirmationOptions.Value.PublicBaseUrl.TrimEnd('/')),
                ContentType = "text/html",
                StatusCode = StatusCodes.Status400BadRequest
            };
        }
    }

    [HttpGet("oauth/{provider}")]
    public IActionResult BeginOAuth(
        string provider,
        [FromQuery] string? returnUrl = null,
        [FromQuery] bool termsAccepted = false,
        [FromQuery] string? termsVersion = null,
        [FromQuery] bool privacyNoticeAcknowledged = false,
        [FromQuery] string? privacyNoticeVersion = null)
    {
        var scheme = NormalizeEnabledOAuthProvider(provider);

        if (scheme is null)
        {
            return NotFound();
        }

        var normalizedReturnUrl = NormalizeReturnUrl(returnUrl);
        var redirectUri = Url.Action(
            nameof(CompleteOAuth),
            values: new { provider = scheme, returnUrl = normalizedReturnUrl }) ?? "/api/auth/me";
        var properties = new AuthenticationProperties
        {
            RedirectUri = redirectUri
        };
        properties.Items[AuthSchemes.OAuthReturnUrlItem] = normalizedReturnUrl;
        properties.Items["legal.termsAccepted"] = termsAccepted.ToString();
        properties.Items["legal.termsVersion"] = termsVersion ?? string.Empty;
        properties.Items["legal.privacyNoticeAcknowledged"] =
            privacyNoticeAcknowledged.ToString();
        properties.Items["legal.privacyNoticeVersion"] = privacyNoticeVersion ?? string.Empty;

        return Challenge(properties, scheme);
    }

    [HttpGet("oauth/{provider}/complete")]
    public async Task<IActionResult> CompleteOAuth(
        string provider,
        [FromQuery] string? returnUrl,
        CancellationToken cancellationToken)
    {
        var scheme = NormalizeEnabledOAuthProvider(provider);

        if (scheme is null)
        {
            return NotFound();
        }

        var result = await HttpContext.AuthenticateAsync(AuthSchemes.ExternalCookie);
        await HttpContext.SignOutAsync(AuthSchemes.ExternalCookie);

        if (!result.Succeeded || result.Principal is null)
        {
            return RedirectWithOAuthError(returnUrl, "external_login_failed");
        }

        var providerUserId = GetExternalProviderUserId(scheme, result.Principal);
        var email = result.Principal.FindFirstValue("email") ??
                    result.Principal.FindFirstValue("preferred_username") ??
                    result.Principal.FindFirstValue(ClaimTypes.Email);

        if (string.IsNullOrWhiteSpace(providerUserId) || string.IsNullOrWhiteSpace(email))
        {
            return RedirectWithOAuthError(returnUrl, "external_login_failed");
        }

        LoginUserResponse response;

        try
        {
            response = await _authService.ExternalLoginAsync(
                new ExternalLoginRequest(
                    scheme,
                    providerUserId,
                    email,
                    result.Principal.FindFirstValue("name") ??
                    result.Principal.FindFirstValue(ClaimTypes.Name),
                    ReadLegalAcceptance(result.Properties)),
                new AuthRequestMetadata(
                    Request.Headers.UserAgent.FirstOrDefault(),
                    HttpContext.Connection.RemoteIpAddress?.ToString()),
                cancellationToken);
        }
        catch (ValidationException exception)
        {
            var errorCode = exception.Message.Contains(
                "Terms of Use",
                StringComparison.OrdinalIgnoreCase)
                ? "legal_acceptance_required"
                : "external_login_failed";

            return RedirectWithOAuthError(returnUrl, errorCode);
        }

        AppendSessionCookie(response);
        return Redirect(NormalizeReturnUrl(returnUrl));
    }

    private static string? GetExternalProviderUserId(
        string provider,
        ClaimsPrincipal principal)
    {
        if (provider.Equals("microsoft", StringComparison.OrdinalIgnoreCase))
        {
            var objectId = principal.FindFirstValue("oid");
            var tenantId = principal.FindFirstValue("tid");

            if (!string.IsNullOrWhiteSpace(objectId) &&
                !string.IsNullOrWhiteSpace(tenantId))
            {
                return $"{tenantId}:{objectId}";
            }
        }

        return principal.FindFirstValue("sub") ??
               principal.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private static LegalAcceptanceSubmission? ReadLegalAcceptance(
        AuthenticationProperties? properties)
    {
        if (properties is null)
        {
            return null;
        }

        var termsAccepted = bool.TryParse(
            GetAuthenticationProperty(properties, "legal.termsAccepted"),
            out var acceptedTerms) && acceptedTerms;
        var privacyAcknowledged = bool.TryParse(
            GetAuthenticationProperty(properties, "legal.privacyNoticeAcknowledged"),
            out var acknowledgedPrivacy) && acknowledgedPrivacy;

        return new LegalAcceptanceSubmission(
            termsAccepted,
            GetAuthenticationProperty(properties, "legal.termsVersion"),
            privacyAcknowledged,
            GetAuthenticationProperty(properties, "legal.privacyNoticeVersion"));
    }

    private static string? GetAuthenticationProperty(
        AuthenticationProperties properties,
        string key) =>
        properties.Items.TryGetValue(key, out var value) ? value : null;

    [EnableRateLimiting("auth")]
    [HttpPost("development-login")]
    public async Task<ActionResult<LoginUserResponse>> DevelopmentLogin(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        try
        {
            var response = await _authService.DevelopmentLoginAsync(
                new AuthRequestMetadata(
                    Request.Headers.UserAgent.FirstOrDefault(),
                    HttpContext.Connection.RemoteIpAddress?.ToString()),
                cancellationToken);

            AppendSessionCookie(response);

            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }
    }

    [EnableRateLimiting("auth")]
    [HttpPost("local-desktop")]
    public async Task<ActionResult<LoginUserResponse>> LocalDesktopLogin(CancellationToken cancellationToken)
    {
        if (!LocalDesktopLoginIsEnabled())
        {
            return NotFound();
        }

        try
        {
            var response = await _authService.LocalDesktopLoginAsync(
                new AuthRequestMetadata(
                    Request.Headers.UserAgent.FirstOrDefault(),
                    HttpContext.Connection.RemoteIpAddress?.ToString()),
                cancellationToken);

            AppendSessionCookie(response);

            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }
    }

    [EnableRateLimiting("auth")]
    [HttpPost("guest")]
    public async Task<ActionResult<LoginUserResponse>> GuestLogin(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _authService.GuestLoginAsync(
                new AuthRequestMetadata(
                    Request.Headers.UserAgent.FirstOrDefault(),
                    HttpContext.Connection.RemoteIpAddress?.ToString()),
                cancellationToken);

            AppendSessionCookie(response);

            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }
    }

    [Authorize(Policy = AuthPolicies.SessionRequired)]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var loggedOut = await _authService.LogoutAsync(cancellationToken);

        if (!loggedOut)
        {
            return Unauthorized();
        }

        Response.Cookies.Delete(SessionCsrfProtectionMiddleware.SessionCookieName);
        Response.Cookies.Delete(SessionCsrfProtectionMiddleware.CsrfCookieName);
        return NoContent();
    }

    [Authorize(Policy = AuthPolicies.SessionRequired)]
    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserResponse>> GetCurrent(CancellationToken cancellationToken)
    {
        var response = await _authService.GetCurrentAsync(cancellationToken);
        return response is null ? Unauthorized() : Ok(response);
    }

    [Authorize(Policy = AuthPolicies.SessionRequired)]
    [HttpGet("sessions")]
    public async Task<ActionResult<IReadOnlyList<AuthSessionListItemResponse>>> ListSessions(
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _authService.ListSessionsAsync(cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    [Authorize(Policy = AuthPolicies.SessionRequired)]
    [HttpDelete("sessions/{sessionId:guid}")]
    public async Task<IActionResult> RevokeSession(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _authService.RevokeSessionAsync(sessionId, cancellationToken);

            if (!response.Revoked)
            {
                return NotFound();
            }

            if (response.CurrentSessionRevoked)
            {
                Response.Cookies.Delete(SessionCsrfProtectionMiddleware.SessionCookieName);
                Response.Cookies.Delete(SessionCsrfProtectionMiddleware.CsrfCookieName);
            }

            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }

    [EnableRateLimiting("auth")]
    [HttpPost("test-email")]
    public async Task<IActionResult> SendTestEmail(
        TestEmailRequest request,
        CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        try
        {
            await _emailSender.SendAsync(
                new EmailMessage(
                    request.Email,
                    null,
                    "DumpTether email test",
                    "<p>Your DumpTether Brevo API configuration can send email.</p>",
                    "Your DumpTether Brevo API configuration can send email."),
                cancellationToken);
            return Accepted(new { message = "Test email sent." });
        }
        catch (EmailDeliveryException exception)
        {
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { error = exception.Message });
        }
    }

    private void AppendSessionCookie(LoginUserResponse response)
    {
        Response.Cookies.Append(
            SessionCsrfProtectionMiddleware.SessionCookieName,
            response.SessionToken,
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Secure = !_environment.IsDevelopment(),
                Expires = response.ExpiresAt
            });
        Response.Cookies.Append(
            SessionCsrfProtectionMiddleware.CsrfCookieName,
            CreateCsrfToken(),
            new CookieOptions
            {
                HttpOnly = false,
                SameSite = SameSiteMode.Strict,
                Secure = !_environment.IsDevelopment(),
                Expires = response.ExpiresAt
            });
    }

    private static string CreateCsrfToken() =>
        WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private string? NormalizeEnabledOAuthProvider(string provider)
    {
        var normalized = provider.Trim().ToLowerInvariant();
        var enabledProviders = _oauthOptions.Value.EnabledProviders();

        return enabledProviders.Contains(normalized, StringComparer.Ordinal)
            ? normalized
            : null;
    }

    private bool LocalDesktopLoginIsEnabled() =>
        _authOptions.Value.EnableLocalDesktopLogin &&
        DumpTetherDatabaseOptions.IsSqlite(DumpTetherDatabaseOptions.GetProvider(_configuration));

    private IActionResult RedirectWithOAuthError(string? returnUrl, string errorCode)
    {
        var destination = QueryHelpers.AddQueryString(
            NormalizeReturnUrl(returnUrl),
            "oauthError",
            errorCode);

        return Redirect(destination);
    }

    private string NormalizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/";
        }

        if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var absolute) &&
            absolute.Scheme is "http" or "https" &&
            (string.Equals(absolute.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase) ||
                (_environment.IsDevelopment() &&
                    (absolute.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                        absolute.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)))))
        {
            return absolute.ToString();
        }

        return returnUrl.StartsWith('/') ? returnUrl : "/";
    }
}
