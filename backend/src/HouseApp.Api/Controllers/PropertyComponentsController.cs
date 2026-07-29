using HouseApp.Api.Authorization;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.PropertyComponents;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HouseApp.Api.Controllers;

/// <summary>
/// The parts of a house a project can concern — shared, global reference data, not scoped to a
/// property. Only admins may change them, since renaming or deleting one changes what every
/// existing project displays for everyone. Reading stays open to all signed-in users — see GetAll.
/// </summary>
[ApiController]
[Route("api/property-components")]
[Authorize]
public class PropertyComponentsController(AppDbContext db) : ControllerBase
{
    /// <summary>
    /// Deliberately NOT admin-gated: this feeds the component dropdown when creating or editing a
    /// project, so gating it would stop regular users logging work at all.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<PropertyComponentDto>>> GetAll()
    {
        var components = await db.PropertyComponents.ToListAsync();
        return Ok(components.OrderBy(c => c.Name).Select(ToDto));
    }

    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    public async Task<ActionResult<PropertyComponentDto>> Create(CreatePropertyComponentRequest request)
    {
        var component = new PropertyComponent
        {
            Name = request.Name,
            RecommendedIntervalMonths = request.RecommendedIntervalMonths,
        };
        db.PropertyComponents.Add(component);
        await db.SaveChangesAsync();
        return Ok(ToDto(component));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = Policies.Admin)]
    public async Task<IActionResult> Update(string id, UpdatePropertyComponentRequest request)
    {
        var component = await db.PropertyComponents.FindAsync(id);
        if (component is null)
        {
            return NotFound();
        }

        component.Name = request.Name;
        component.RecommendedIntervalMonths = request.RecommendedIntervalMonths;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = Policies.Admin)]
    public async Task<IActionResult> Delete(string id)
    {
        var component = await db.PropertyComponents.FindAsync(id);
        if (component is null)
        {
            return NotFound();
        }

        // Full container scan + in-memory check rather than a Cosmos query filtered by ComponentId
        // (not the partition key) — same reasoning as the other Cosmos query-shape notes in
        // CLAUDE.md. projects stays small enough for this to be fine.
        var allProjects = await db.Projects.ToListAsync();
        if (allProjects.Any(p => p.ComponentId == id))
        {
            return Conflict(new { message = "Component is used by one or more projects and can't be deleted." });
        }

        db.PropertyComponents.Remove(component);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static PropertyComponentDto ToDto(PropertyComponent c) =>
        new(c.Id, c.Name, c.RecommendedIntervalMonths);
}
