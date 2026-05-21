using System.ComponentModel.DataAnnotations;
using DumpTether.App.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace DumpTether.Api.Controllers;

[ApiController]
[Route("api/tasks")]
public sealed class TaskItemsController : ControllerBase
{
    private readonly ITaskItemService _taskItemService;

    public TaskItemsController(ITaskItemService taskItemService)
    {
        _taskItemService = taskItemService;
    }

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
        [FromQuery] string? archive,
        [FromQuery] string? followUp,
        [FromQuery] int? notViewedSinceDays,
        [FromQuery] int? notTouchedSinceDays,
        [FromQuery] string? text,
        [FromQuery] string? sort,
        [FromQuery] string? direction,
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
                    archive,
                    followUp,
                    notViewedSinceDays,
                    notTouchedSinceDays,
                    text,
                    sort,
                    direction),
                cancellationToken);
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

    [HttpPost("{id:guid}/archive")]
    public async Task<ActionResult<TaskItemDetailResponse>> Archive(
        Guid id,
        ArchiveTaskItemRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _taskItemService.ArchiveAsync(id, request, cancellationToken);
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
}
