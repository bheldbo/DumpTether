using DumpTether.App.ArchiveResolutions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DumpTether.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.SessionRequired)]
[Route("api/archive-resolutions")]
public sealed class ArchiveResolutionsController : ControllerBase
{
    private readonly IArchiveResolutionService _archiveResolutionService;

    public ArchiveResolutionsController(IArchiveResolutionService archiveResolutionService)
    {
        _archiveResolutionService = archiveResolutionService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ArchiveResolutionResponse>>> List(
        CancellationToken cancellationToken)
    {
        var response = await _archiveResolutionService.ListAsync(cancellationToken);
        return Ok(response);
    }

    [HttpPost]
    public async Task<ActionResult<ArchiveResolutionResponse>> Create(
        CreateArchiveResolutionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _archiveResolutionService.CreateAsync(request, cancellationToken);
            return Created($"/api/archive-resolutions/{response.Id}", response);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPatch("{id:guid}")]
    public async Task<ActionResult<ArchiveResolutionResponse>> Update(
        Guid id,
        UpdateArchiveResolutionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _archiveResolutionService.UpdateAsync(
                id,
                request,
                cancellationToken);

            return response is null ? NotFound() : Ok(response);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var wasDeleted = await _archiveResolutionService.DeactivateAsync(id, cancellationToken);
        return wasDeleted ? NoContent() : NotFound();
    }
}
