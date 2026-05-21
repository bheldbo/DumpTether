using DumpTether.App.ArchiveResolutions;
using Microsoft.AspNetCore.Mvc;

namespace DumpTether.Api.Controllers;

[ApiController]
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
}
