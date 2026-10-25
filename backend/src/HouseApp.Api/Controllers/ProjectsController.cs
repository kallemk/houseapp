using HouseApp.Api.Data;
using HouseApp.Api.Extensions;
using HouseApp.Api.Dtos.Projects;
using HouseApp.Api.Models;
using HouseApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseApp.Api.Controllers;

/// <summary>
/// A project is a single Cosmos document with its costs and contractor nested inside, so it is read
/// and written whole — there are no sub-resource endpoints for costs. Update/delete take propertyId
/// as a query-string parameter because it's the partition key, matching the other per-property
/// controllers.
/// </summary>
[ApiController]
[Authorize]
public class ProjectsController(AppDbContext db, ProjectService projects) : ControllerBase
{
    [HttpGet("api/properties/{propertyId}/projects")]
    public async Task<ActionResult<List<ProjectDto>>> GetForProperty(string propertyId)
    {
        if (!await db.CanAccessPropertyAsync(propertyId, User.CurrentUserId()))
        {
            return NotFound();
        }

        return Ok((await projects.ListAsync(propertyId)).Select(ProjectService.ToDto));
    }

    [HttpGet("api/projects/{id}")]
    public async Task<ActionResult<ProjectDto>> GetById(string id, [FromQuery] string propertyId)
    {
        if (!await db.CanAccessPropertyAsync(propertyId, User.CurrentUserId()))
        {
            return NotFound();
        }

        var project = await projects.FindAsync(propertyId, id);
        return project is null ? NotFound() : Ok(ProjectService.ToDto(project));
    }

    [HttpPost("api/properties/{propertyId}/projects")]
    public async Task<ActionResult<ProjectDto>> Create(string propertyId, SaveProjectRequest request)
    {
        if (!await db.CanAccessPropertyAsync(propertyId, User.CurrentUserId()))
        {
            return NotFound();
        }

        var project = new Project
        {
            PropertyId = propertyId,
            Name = request.Name,
            ComponentId = request.ComponentId,
            CreatedByUserId = User.CurrentUserId(),
        };
        ProjectService.Apply(project, request);

        try
        {
            await projects.CreateAsync(project);
        }
        catch (LimitReachedException ex)
        {
            return Limits.Reached(ex.Message);
        }

        return Ok(ProjectService.ToDto(project));
    }

    [HttpPut("api/projects/{id}")]
    public async Task<IActionResult> Update(
        string id,
        [FromQuery] string propertyId,
        SaveProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await db.CanAccessPropertyAsync(propertyId, User.CurrentUserId()))
        {
            return NotFound();
        }

        var project = await projects.FindAsync(propertyId, id);
        if (project is null)
        {
            return NotFound();
        }

        try
        {
            await projects.UpdateAsync(project, p => ProjectService.Apply(p, request), cancellationToken);
        }
        catch (LimitReachedException ex)
        {
            return Limits.Reached(ex.Message);
        }

        return NoContent();
    }

    [HttpDelete("api/projects/{id}")]
    public async Task<IActionResult> Delete(string id, [FromQuery] string propertyId)
    {
        if (!await db.CanAccessPropertyAsync(propertyId, User.CurrentUserId()))
        {
            return NotFound();
        }

        var project = await projects.FindAsync(propertyId, id);
        if (project is null)
        {
            return NotFound();
        }

        db.Projects.Remove(project);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
