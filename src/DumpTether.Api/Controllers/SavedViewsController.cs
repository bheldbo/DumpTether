using System.ComponentModel.DataAnnotations;
using DumpTether.App.Views;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DumpTether.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.SessionRequired)]
[Route("api/views")]
public sealed class SavedViewsController : ControllerBase
{
    private readonly ISavedViewService _savedViewService;

    public SavedViewsController(ISavedViewService savedViewService)
    {
        _savedViewService = savedViewService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SavedViewResponse>>> List(
        CancellationToken cancellationToken)
    {
        var response = await _savedViewService.ListAsync(cancellationToken);
        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SavedViewResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await _savedViewService.GetByIdAsync(id, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<SavedViewResponse>> Create(
        CreateSavedViewRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _savedViewService.CreateAsync(request, cancellationToken);
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
    public async Task<ActionResult<SavedViewResponse>> Update(
        Guid id,
        UpdateSavedViewRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _savedViewService.UpdateAsync(id, request, cancellationToken);
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
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var deleted = await _savedViewService.DeleteAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}
