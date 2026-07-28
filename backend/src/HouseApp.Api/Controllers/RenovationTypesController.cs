using HouseApp.Api.Authorization;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.RenovationTypes;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HouseApp.Api.Controllers;

/// <summary>
/// Renovation types are shared, global reference data (not scoped to a property) — the admin
/// interface for what used to be the hardcoded RenovationCategory enum. Only admins may change
/// them, since renaming or deleting a type changes what every existing entry displays for
/// everyone. Reading stays open to all signed-in users — see GetAll.
/// </summary>
[ApiController]
[Route("api/renovation-types")]
[Authorize]
public class RenovationTypesController(AppDbContext db) : ControllerBase
{
    /// <summary>
    /// Deliberately NOT admin-gated: this feeds the type dropdown on the renovations page and in
    /// the dashboard quick-add modal, so gating it would stop regular users creating entries at all.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<RenovationTypeDto>>> GetAll()
    {
        var types = await db.RenovationTypes.ToListAsync();
        return Ok(types.OrderBy(t => t.Name).Select(ToDto));
    }

    [HttpPost]
    [Authorize(Policy = Policies.Admin)]
    public async Task<ActionResult<RenovationTypeDto>> Create(CreateRenovationTypeRequest request)
    {
        var type = new RenovationType
        {
            Name = request.Name,
            RecommendedIntervalMonths = request.RecommendedIntervalMonths,
        };
        db.RenovationTypes.Add(type);
        await db.SaveChangesAsync();
        return Ok(ToDto(type));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = Policies.Admin)]
    public async Task<IActionResult> Update(string id, UpdateRenovationTypeRequest request)
    {
        var type = await db.RenovationTypes.FindAsync(id);
        if (type is null)
        {
            return NotFound();
        }

        type.Name = request.Name;
        type.RecommendedIntervalMonths = request.RecommendedIntervalMonths;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = Policies.Admin)]
    public async Task<IActionResult> Delete(string id)
    {
        var type = await db.RenovationTypes.FindAsync(id);
        if (type is null)
        {
            return NotFound();
        }

        // Full container scan + in-memory check rather than a Cosmos query filtered by
        // RenovationTypeId (not the partition key) — same reasoning as the other Cosmos query-shape
        // notes in CLAUDE.md. renovationEntries stays small enough for this to be fine.
        var allEntries = await db.RenovationEntries.ToListAsync();
        if (allEntries.Any(r => r.RenovationTypeId == id))
        {
            return Conflict(new { message = "Renovation type is used by one or more entries and can't be deleted." });
        }

        db.RenovationTypes.Remove(type);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static RenovationTypeDto ToDto(RenovationType t) => new(t.Id, t.Name, t.RecommendedIntervalMonths);
}
