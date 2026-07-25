using System.Security.Claims;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Valuations;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HouseApp.Api.Controllers;

[ApiController]
[Authorize]
public class ValuationsController(AppDbContext db) : ControllerBase
{
    [HttpGet("api/properties/{propertyId}/valuations")]
    public async Task<ActionResult<List<ValuationEntryDto>>> GetForProperty(string propertyId)
    {
        var entries = await db.ValuationEntries
            .Where(v => v.PropertyId == propertyId)
            .OrderByDescending(v => v.Date)
            .ToListAsync();
        return Ok(entries.Select(ToDto));
    }

    [HttpPost("api/properties/{propertyId}/valuations")]
    public async Task<ActionResult<ValuationEntryDto>> Create(string propertyId, CreateValuationEntryRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var entry = new ValuationEntry
        {
            PropertyId = propertyId,
            Date = request.Date,
            Value = request.Value,
            Source = request.Source,
            Notes = request.Notes,
            CreatedByUserId = userId,
        };
        db.ValuationEntries.Add(entry);
        await db.SaveChangesAsync();
        return Ok(ToDto(entry));
    }

    [HttpPut("api/valuations/{id}")]
    public async Task<IActionResult> Update(string id, [FromQuery] string propertyId, UpdateValuationEntryRequest request)
    {
        var entry = await db.ValuationEntries
            .Where(v => v.PropertyId == propertyId && v.Id == id)
            .FirstOrDefaultAsync();
        if (entry is null)
        {
            return NotFound();
        }

        entry.Date = request.Date;
        entry.Value = request.Value;
        entry.Source = request.Source;
        entry.Notes = request.Notes;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("api/valuations/{id}")]
    public async Task<IActionResult> Delete(string id, [FromQuery] string propertyId)
    {
        var entry = await db.ValuationEntries
            .Where(v => v.PropertyId == propertyId && v.Id == id)
            .FirstOrDefaultAsync();
        if (entry is null)
        {
            return NotFound();
        }

        db.ValuationEntries.Remove(entry);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static ValuationEntryDto ToDto(ValuationEntry v) =>
        new(v.Id, v.PropertyId, v.Date, v.Value, v.Source, v.Notes, v.CreatedByUserId, v.CreatedAt);
}
