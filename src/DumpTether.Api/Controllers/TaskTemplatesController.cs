using System.ComponentModel.DataAnnotations;
using DumpTether.App.Templates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DumpTether.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.SessionRequired)]
[Route("api/templates")]
public sealed class TaskTemplatesController : ControllerBase
{
    private readonly ITaskTemplateService _taskTemplateService;

    public TaskTemplatesController(ITaskTemplateService taskTemplateService)
    {
        _taskTemplateService = taskTemplateService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TaskTemplateSummaryResponse>>> List(
        CancellationToken cancellationToken)
    {
        var response = await _taskTemplateService.ListAsync(cancellationToken);
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TaskTemplateDetailResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await _taskTemplateService.GetByIdAsync(id, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.WorkspaceWriteRequired)]
    public async Task<ActionResult<TaskTemplateDetailResponse>> Create(
        CreateTaskTemplateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _taskTemplateService.CreateAsync(request, cancellationToken);
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

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = AuthPolicies.WorkspaceWriteRequired)]
    public async Task<ActionResult<TaskTemplateDetailResponse>> Update(
        Guid id,
        UpdateTaskTemplateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _taskTemplateService.UpdateAsync(id, request, cancellationToken);
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

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthPolicies.WorkspaceWriteRequired)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var deleted = await _taskTemplateService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
