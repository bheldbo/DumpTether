using DumpTether.App.Workspaces;
using Microsoft.AspNetCore.Mvc;

namespace DumpTether.Api.Controllers;

[ApiController]
[Route("api/workspace")]
public sealed class WorkspaceController : ControllerBase
{
    private readonly IWorkspaceService _workspaceService;

    public WorkspaceController(IWorkspaceService workspaceService)
    {
        _workspaceService = workspaceService;
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
    }
}
