using System.ComponentModel.DataAnnotations;
using DumpTether.App.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace DumpTether.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IWebHostEnvironment _environment;
    private readonly IOptions<AuthOptions> _authOptions;

    public AuthController(
        IAuthService authService,
        IWebHostEnvironment environment,
        IOptions<AuthOptions> authOptions)
    {
        _authService = authService;
        _environment = environment;
        _authOptions = authOptions;
    }

    [HttpGet("options")]
    public ActionResult<AuthClientOptionsResponse> GetOptions()
    {
        var options = _authOptions.Value;
        return Ok(new AuthClientOptionsResponse(
            options.RequireAuthentication,
            options.AllowGuestSessions,
            _environment.IsDevelopment() && options.EnableDevelopmentLogin));
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
    }

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

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var loggedOut = await _authService.LogoutAsync(cancellationToken);

        if (!loggedOut)
        {
            return Unauthorized();
        }

        Response.Cookies.Delete("DumpTether.Session");
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserResponse>> GetCurrent(CancellationToken cancellationToken)
    {
        var response = await _authService.GetCurrentAsync(cancellationToken);
        return response is null ? Unauthorized() : Ok(response);
    }

    private void AppendSessionCookie(LoginUserResponse response)
    {
        // TODO auth-hardening: if the frontend switches from bearer storage to cookies,
        // add CSRF protection before relying on cookie auth for state-changing requests.
        Response.Cookies.Append(
            "DumpTether.Session",
            response.SessionToken,
            new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Secure = !_environment.IsDevelopment(),
                Expires = response.ExpiresAt
            });
    }
}
