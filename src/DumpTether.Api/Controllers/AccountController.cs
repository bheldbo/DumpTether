using System.ComponentModel.DataAnnotations;
using DumpTether.App.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DumpTether.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.SessionRequired)]
[Route("api/account")]
public sealed class AccountController : ControllerBase
{
    private readonly IAuthService _authService;

    public AccountController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet("deletion")]
    public async Task<IActionResult> GetDeletion(CancellationToken cancellationToken)
    {
        var status = await _authService.GetAccountDeletionStatusAsync(cancellationToken);
        return status is null ? NoContent() : Ok(status);
    }

    [EnableRateLimiting("auth")]
    [HttpPost("deletion")]
    public async Task<ActionResult<AccountDeletionStatusResponse>> RequestDeletion(
        RequestAccountDeletionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _authService.RequestAccountDeletionAsync(request, cancellationToken));
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

    [HttpDelete("deletion")]
    public async Task<IActionResult> CancelDeletion(CancellationToken cancellationToken)
    {
        return await _authService.CancelAccountDeletionAsync(cancellationToken)
            ? NoContent()
            : NotFound();
    }
}
