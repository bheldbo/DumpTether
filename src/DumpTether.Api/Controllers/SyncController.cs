using System.ComponentModel.DataAnnotations;
using DumpTether.App.Sync;
using DumpTether.App.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DumpTether.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.SessionRequired)]
[Route("api/sync")]
public sealed class SyncController : ControllerBase
{
    private readonly IHostEnvironment _environment;
    private readonly ISyncService _syncService;

    public SyncController(
        IHostEnvironment environment,
        ISyncService syncService)
    {
        _environment = environment;
        _syncService = syncService;
    }

    [HttpGet("workspace-roots")]
    public async Task<ActionResult<IReadOnlyList<SyncRootResponse>>> ListWorkspaceRoots(
        CancellationToken cancellationToken)
    {
        if (!IsDesktopEnvironment())
        {
            return NotFound();
        }

        var response = await _syncService.ListWorkspaceRootsAsync(cancellationToken);
        return Ok(response);
    }

    [HttpPost("workspace-roots")]
    public async Task<ActionResult<SyncRootResponse>> EnsureWorkspaceRoot(
        EnsureWorkspaceSyncRootRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsDesktopEnvironment())
        {
            return NotFound();
        }

        try
        {
            var response = await _syncService.EnsureWorkspaceRootAsync(
                request,
                cancellationToken);

            return Ok(response);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (UnauthorizedAccessException exception)
        {
            return Unauthorized(new { error = exception.Message });
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPost("workspace-roots/link")]
    public async Task<ActionResult<SyncRootResponse>> LinkWorkspaceRoot(
        LinkWorkspaceSyncRootRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsDesktopEnvironment())
        {
            return NotFound();
        }

        try
        {
            var response = await _syncService.LinkWorkspaceRootAsync(
                request,
                cancellationToken);

            return Ok(response);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (UnauthorizedAccessException exception)
        {
            return Unauthorized(new { error = exception.Message });
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPost("workspaces/{workspaceId:guid}/tasks/{taskItemId:guid}/synced")]
    public async Task<ActionResult<TaskSyncStateResponse>> MarkTaskItemSynced(
        Guid workspaceId,
        Guid taskItemId,
        MarkTaskItemSyncedRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsDesktopEnvironment())
        {
            return NotFound();
        }

        try
        {
            var response = await _syncService.MarkTaskItemSyncedAsync(
                workspaceId,
                taskItemId,
                request,
                cancellationToken);

            return Ok(response);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (UnauthorizedAccessException exception)
        {
            return Unauthorized(new { error = exception.Message });
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPost("workspaces/{workspaceId:guid}/tasks/{taskItemId:guid}/failed")]
    public async Task<ActionResult<TaskSyncStateResponse>> MarkTaskItemSyncFailed(
        Guid workspaceId,
        Guid taskItemId,
        MarkTaskItemSyncFailedRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsDesktopEnvironment())
        {
            return NotFound();
        }

        try
        {
            var response = await _syncService.MarkTaskItemSyncFailedAsync(
                workspaceId,
                taskItemId,
                request,
                cancellationToken);

            return Ok(response);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (UnauthorizedAccessException exception)
        {
            return Unauthorized(new { error = exception.Message });
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPost("workspaces/{workspaceId:guid}/run")]
    public async Task<ActionResult<SyncWorkspaceWithCloudResponse>> SyncWorkspaceWithCloud(
        Guid workspaceId,
        SyncWorkspaceWithCloudRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsDesktopEnvironment())
        {
            return NotFound();
        }

        try
        {
            var response = await _syncService.SyncWorkspaceWithCloudAsync(
                workspaceId,
                request,
                cancellationToken);

            return Ok(response);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (UnauthorizedAccessException exception)
        {
            return Unauthorized(new { error = exception.Message });
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    private bool IsDesktopEnvironment()
    {
        return string.Equals(
            _environment.EnvironmentName,
            "Desktop",
            StringComparison.OrdinalIgnoreCase);
    }
}
