using System.ComponentModel.DataAnnotations;
using DumpTether.App.Projects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DumpTether.Api.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicies.SessionRequired)]
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

    [HttpPost]
    [Authorize(Policy = AuthPolicies.WorkspaceWriteRequired)]
    public async Task<ActionResult<ProjectResponse>> Create(
        CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _projectService.CreateAsync(request, cancellationToken);
            return Created($"/api/projects/{response.Id}", response);
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
    public async Task<ActionResult<ProjectResponse>> Update(
        Guid id,
        UpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _projectService.UpdateAsync(id, request, cancellationToken);

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
    public async Task<ActionResult<ProjectArchiveResponse>> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await _projectService.DeleteAsync(id, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }

    [HttpPost("{id:guid}/archive-tasks")]
    [Authorize(Policy = AuthPolicies.WorkspaceWriteRequired)]
    public async Task<ActionResult<ProjectArchiveResponse>> ArchiveTasksAndDeactivate(
        Guid id,
        ArchiveProjectTasksRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _projectService.ArchiveTasksAndDeactivateAsync(
                id,
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
}
