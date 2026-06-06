using System.ComponentModel.DataAnnotations;
using DumpTether.App.Tasks;
using DumpTether.App.Workspaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DumpTether.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.SessionRequired)]
[Route("api/share-links")]
public sealed class ShareLinksController : ControllerBase
{
    private readonly ITaskItemService _taskItemService;
    private readonly IWorkspaceService _workspaceService;

    public ShareLinksController(
        ITaskItemService taskItemService,
        IWorkspaceService workspaceService)
    {
        _taskItemService = taskItemService;
        _workspaceService = workspaceService;
    }

    [EnableRateLimiting("auth")]
    [HttpPost("accept")]
    public async Task<ActionResult<ShareLinkAcceptResponse>> Accept(
        AcceptShareLinkRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new { error = "Share token is required." });
        }

        try
        {
            var workspaceInvitation = await _workspaceService.AcceptInvitationTokenAsync(
                request.Token,
                cancellationToken);

            return Ok(new ShareLinkAcceptResponse(
                "Workspace",
                workspaceInvitation.WorkspaceId,
                []));
        }
        catch (ValidationException)
        {
            // The token may be a task-share link. Try that before returning the generic failure.
        }

        try
        {
            var response = await _taskItemService.AcceptShareLinkAsync(request, cancellationToken);
            return Ok(response);
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
}
