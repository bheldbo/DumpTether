using System.ComponentModel.DataAnnotations;
using DumpTether.App.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DumpTether.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.SessionRequired)]
[Route("api/tasks")]
public sealed class TaskItemsController : ControllerBase
{
    private readonly ITaskItemService _taskItemService;

    public TaskItemsController(ITaskItemService taskItemService)
    {
        _taskItemService = taskItemService;
    }

    [EnableRateLimiting("task-writes")]
    [HttpPost]
    public async Task<ActionResult<TaskItemDetailResponse>> Create(
        CreateTaskItemRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _taskItemService.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
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

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TaskItemSummaryResponse>>> List(
        [FromQuery] TaskItemListScope scope,
        [FromQuery] Guid? viewId,
        [FromQuery] Guid? projectId,
        [FromQuery] string? status,
        [FromQuery] string? category,
        [FromQuery] string? color,
        [FromQuery] string? archive,
        [FromQuery] string? followUp,
        [FromQuery] int? notViewedSinceDays,
        [FromQuery] int? notTouchedSinceDays,
        [FromQuery] string? text,
        [FromQuery] string? sharedWith,
        [FromQuery] bool sharedWithMe,
        [FromQuery] string? sort,
        [FromQuery] string? direction,
        [FromQuery] bool includeChildTasks,
        CancellationToken cancellationToken)
    {
        if (scope == 0)
        {
            scope = TaskItemListScope.Active;
        }

        try
        {
            var response = await _taskItemService.ListAsync(
                new TaskItemListRequest(
                    viewId,
                    scope,
                    projectId,
                    status,
                    category,
                    color,
                    archive,
                    followUp,
                    notViewedSinceDays,
                    notTouchedSinceDays,
                    text,
                    sharedWith,
                    sharedWithMe,
                    sort,
                    direction,
                    includeChildTasks),
                cancellationToken);
            return Ok(response);
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [EnableRateLimiting("task-writes")]
    [HttpPost("copy")]
    public async Task<ActionResult<CopyTaskItemsResponse>> Copy(
        CopyTaskItemsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _taskItemService.CopyAsync(request, cancellationToken);
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

    [HttpGet("view-counts")]
    public async Task<ActionResult<IReadOnlyList<TaskItemViewCountResponse>>> CountByViews(
        [FromQuery] Guid[] viewIds,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _taskItemService.CountByViewsAsync(viewIds, cancellationToken);
            return Ok(response);
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TaskItemDetailResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await _taskItemService.GetByIdAsync(id, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet("{id:guid}/subtasks")]
    public async Task<ActionResult<IReadOnlyList<TaskItemSummaryResponse>>> ListSubtasks(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await _taskItemService.ListSubtasksAsync(id, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [EnableRateLimiting("task-writes")]
    [HttpPost("{id:guid}/subtasks")]
    public async Task<ActionResult<TaskItemDetailResponse>> CreateSubtask(
        Guid id,
        CreateTaskItemRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _taskItemService.CreateSubtaskAsync(id, request, cancellationToken);
            return response is null
                ? NotFound()
                : CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [EnableRateLimiting("task-writes")]
    [HttpPost("{id:guid}/template/import")]
    public async Task<ActionResult<TaskTemplateImportResponse>> ImportTemplate(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _taskItemService.ImportTemplateAsync(id, cancellationToken);
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

    [EnableRateLimiting("task-writes")]
    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<TaskItemDetailResponse>> Update(
        Guid id,
        UpdateTaskItemRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _taskItemService.UpdateAsync(id, request, cancellationToken);
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

    [EnableRateLimiting("task-writes")]
    [HttpPost("{id:guid}/timeline")]
    public async Task<ActionResult<TaskItemDetailResponse>> AddTimelineEntry(
        Guid id,
        AddTaskTimelineEntryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _taskItemService.AddTimelineEntryAsync(id, request, cancellationToken);
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

    [EnableRateLimiting("task-writes")]
    [HttpPatch("{id:guid}/timeline/{entryId:guid}")]
    public async Task<ActionResult<TaskItemDetailResponse>> UpdateTimelineEntry(
        Guid id,
        Guid entryId,
        UpdateTaskTimelineEntryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _taskItemService.UpdateTimelineEntryAsync(
                id,
                entryId,
                request,
                cancellationToken);
            return response is null ? NotFound() : Ok(response);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [EnableRateLimiting("task-writes")]
    [HttpDelete("{id:guid}/timeline/{entryId:guid}")]
    public async Task<ActionResult<TaskItemDetailResponse>> DeleteTimelineEntry(
        Guid id,
        Guid entryId,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _taskItemService.DeleteTimelineEntryAsync(
                id,
                entryId,
                cancellationToken);
            return response is null ? NotFound() : Ok(response);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [EnableRateLimiting("task-writes")]
    [HttpPost("{id:guid}/archive")]
    public async Task<ActionResult<TaskItemDetailResponse>> Archive(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _taskItemService.ArchiveAsync(id, cancellationToken);
            return response is null ? NotFound() : Ok(response);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [EnableRateLimiting("task-writes")]
    [HttpPost("{id:guid}/reopen")]
    public async Task<ActionResult<TaskItemDetailResponse>> Reopen(
        Guid id,
        ReopenTaskItemRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _taskItemService.ReopenAsync(id, request, cancellationToken);
            return response is null ? NotFound() : Ok(response);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [EnableRateLimiting("task-writes")]
    [HttpPost("reopen")]
    public async Task<ActionResult<TaskItemBatchResponse>> ReopenMany(
        ReopenTaskItemsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _taskItemService.ReopenAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [EnableRateLimiting("task-writes")]
    [HttpPost("permanent-delete")]
    public async Task<ActionResult<TaskItemBatchResponse>> DeleteArchived(
        DeleteTaskItemsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _taskItemService.DeleteArchivedAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpGet("{id:guid}/shares")]
    public async Task<ActionResult<IReadOnlyList<TaskItemShareResponse>>> ListShares(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await _taskItemService.ListSharesAsync(id, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpGet("/api/account/task-shares")]
    public async Task<ActionResult<IReadOnlyList<TaskShareInboxResponse>>> ListIncomingShares(
        CancellationToken cancellationToken)
    {
        var response = await _taskItemService.ListIncomingSharesAsync(cancellationToken);
        return Ok(response);
    }

    [EnableRateLimiting("task-writes")]
    [HttpPost("{id:guid}/shares")]
    public async Task<ActionResult<TaskItemDetailResponse>> Share(
        Guid id,
        CreateTaskShareRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _taskItemService.ShareAsync(id, request, cancellationToken);
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

    [EnableRateLimiting("task-writes")]
    [HttpPost("{id:guid}/share-links")]
    public async Task<ActionResult<TaskShareLinkResponse>> CreateShareLink(
        Guid id,
        CreateTaskShareRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _taskItemService.CreateShareLinkAsync(id, request, cancellationToken);
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

    [EnableRateLimiting("task-writes")]
    [HttpPost("share-links")]
    public async Task<ActionResult<TaskShareLinkResponse>> CreateShareLink(
        CreateTaskShareLinkRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _taskItemService.CreateShareLinkAsync(request, cancellationToken);
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

    [EnableRateLimiting("task-writes")]
    [HttpDelete("{id:guid}/shares/{shareId:guid}")]
    public async Task<ActionResult<TaskItemDetailResponse>> RevokeShare(
        Guid id,
        Guid shareId,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _taskItemService.RevokeShareAsync(
                id,
                shareId,
                cancellationToken);
            return response is null ? NotFound() : Ok(response);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [EnableRateLimiting("task-writes")]
    [HttpPatch("{id:guid}/shares/{shareId:guid}")]
    public async Task<ActionResult<TaskItemDetailResponse>> UpdateShareRole(
        Guid id,
        Guid shareId,
        UpdateTaskShareRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _taskItemService.UpdateShareRoleAsync(
                id,
                shareId,
                request,
                cancellationToken);
            return response is null ? NotFound() : Ok(response);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (ValidationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [EnableRateLimiting("task-writes")]
    [HttpDelete("/api/account/task-shares/{shareId:guid}")]
    public async Task<IActionResult> LeaveShare(
        Guid shareId,
        CancellationToken cancellationToken)
    {
        try
        {
            var left = await _taskItemService.LeaveShareAsync(shareId, cancellationToken);
            return left ? NoContent() : NotFound();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [EnableRateLimiting("task-writes")]
    [HttpDelete("/api/account/workspaces/{workspaceId:guid}/task-shares")]
    public async Task<IActionResult> LeaveWorkspaceShares(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var revokedCount = await _taskItemService.LeaveWorkspaceSharesAsync(
                workspaceId,
                cancellationToken);
            return revokedCount == 0 ? NotFound() : NoContent();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }
}
