using System.ComponentModel.DataAnnotations;
using DumpTether.App.Workspaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DumpTether.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.SessionRequired)]
[Route("api/workspace")]
public sealed class WorkspaceController : ControllerBase
{
    private readonly IWorkspaceService _workspaceService;

    public WorkspaceController(IWorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
    }

    [HttpGet("/api/workspaces")]
    public async Task<ActionResult<IReadOnlyList<WorkspaceResponse>>> List(
        CancellationToken cancellationToken)
    {
        var response = await _workspaceService.ListAsync(cancellationToken);
        return Ok(response);
    }

    [HttpGet]
    public async Task<ActionResult<WorkspaceResponse>> GetCurrent(
        CancellationToken cancellationToken)
    {
        var response = await _workspaceService.GetCurrentAsync(cancellationToken);
        return Ok(response);
    }

    [HttpPatch]
    public async Task<ActionResult<WorkspaceResponse>> UpdateCurrent(
        UpdateWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _workspaceService.UpdateCurrentAsync(request, cancellationToken);
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

    [HttpPatch("/api/workspaces/{workspaceId:guid}")]
    public async Task<ActionResult<WorkspaceResponse>> Update(
        Guid workspaceId,
        UpdateWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _workspaceService.UpdateAsync(
                workspaceId,
                request,
                cancellationToken);
            return response is null ? NotFound() : Ok(response);
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

    [HttpGet("members")]
    public async Task<ActionResult<IReadOnlyList<WorkspaceMemberResponse>>> ListMembers(
        CancellationToken cancellationToken)
    {
        var response = await _workspaceService.ListMembersAsync(cancellationToken);
        return Ok(response);
    }

    [HttpGet("invitations")]
    public async Task<ActionResult<IReadOnlyList<WorkspaceInvitationResponse>>> ListInvitations(
        CancellationToken cancellationToken)
    {
        var response = await _workspaceService.ListInvitationsAsync(cancellationToken);
        return Ok(response);
    }

    [HttpGet("/api/account/invitations")]
    public async Task<ActionResult<IReadOnlyList<WorkspaceInvitationInboxResponse>>> ListIncomingInvitations(
        CancellationToken cancellationToken)
    {
        var response = await _workspaceService.ListIncomingInvitationsAsync(cancellationToken);
        return Ok(response);
    }

    [EnableRateLimiting("auth")]
    [HttpPost("invitations")]
    public async Task<ActionResult<WorkspaceInvitationResponse>> CreateInvitation(
        CreateWorkspaceInvitationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _workspaceService.CreateInvitationAsync(request, cancellationToken);
            return Created($"/api/workspace/invitations/{response.Id}", response);
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
    [HttpPost("invitations/accept")]
    public async Task<ActionResult<WorkspaceInvitationResponse>> AcceptInvitation(
        AcceptWorkspaceInvitationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _workspaceService.AcceptInvitationAsync(request, cancellationToken);
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

    [EnableRateLimiting("auth")]
    [HttpPost("/api/account/invitations/{invitationId:guid}/accept")]
    public async Task<ActionResult<WorkspaceInvitationResponse>> AcceptIncomingInvitation(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _workspaceService.AcceptIncomingInvitationAsync(
                invitationId,
                cancellationToken);
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

    [HttpDelete("/api/account/invitations/{invitationId:guid}")]
    public async Task<IActionResult> DeclineIncomingInvitation(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        var declined = await _workspaceService.DeclineIncomingInvitationAsync(
            invitationId,
            cancellationToken);

        return declined ? NoContent() : NotFound();
    }

    [HttpDelete("invitations/{invitationId:guid}")]
    public async Task<IActionResult> RevokeInvitation(
        Guid invitationId,
        CancellationToken cancellationToken)
    {
        var revoked = await _workspaceService.RevokeInvitationAsync(
            invitationId,
            cancellationToken);

        return revoked ? NoContent() : NotFound();
    }

    [HttpDelete("members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            var removed = await _workspaceService.RemoveMemberAsync(
                userId,
                cancellationToken);

            return removed ? NoContent() : NotFound();
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

    [HttpDelete("membership")]
    public async Task<IActionResult> LeaveCurrentWorkspace(CancellationToken cancellationToken)
    {
        try
        {
            var left = await _workspaceService.LeaveCurrentWorkspaceAsync(cancellationToken);
            return left ? NoContent() : NotFound();
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPost("/api/workspaces")]
    public async Task<ActionResult<WorkspaceResponse>> Create(
        CreateWorkspaceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _workspaceService.CreateAsync(request, cancellationToken);
            return Created($"/api/workspaces/{response.Id}", response);
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

    [HttpDelete("/api/workspaces/{workspaceId:guid}")]
    public async Task<IActionResult> Delete(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _workspaceService.DeleteAsync(
                workspaceId,
                cancellationToken);

            return deleted ? NoContent() : NotFound();
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
