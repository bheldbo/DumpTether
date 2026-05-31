using System.ComponentModel.DataAnnotations;
using DumpTether.App.Auth;
using Microsoft.AspNetCore.Mvc;

namespace DumpTether.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IWebHostEnvironment _environment;

    public AuthController(IAuthService authService, IWebHostEnvironment environment)
    {
        _authService = authService;
        _environment = environment;
    }

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
}
