using DumpTether.App.Projects;
using Microsoft.AspNetCore.Mvc;

namespace DumpTether.Api.Controllers;

[ApiController]
[Route("api/projects")]
public sealed class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;

    public ProjectsController(IProjectService projectService)
    {
        _projectService = projectService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProjectResponse>>> List(
        CancellationToken cancellationToken)
    {
        var response = await _projectService.ListAsync(cancellationToken);
        return Ok(response);
    }
}
