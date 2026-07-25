using System.Security.Claims;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.RenovationEntries;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HouseApp.Api.Controllers;

[ApiController]
[Authorize]
public class RenovationEntriesController(AppDbContext db) : ControllerBase
{
    [HttpGet("api/properties/{propertyId}/renovation-entries")]
    public async Task<ActionResult<List<RenovationEntryDto>>> GetForProperty(string propertyId)
    {
        var entries = await db.RenovationEntries
            .Where(r => r.PropertyId == propertyId)
            .OrderByDescending(r => r.Date)
            .ToListAsync();
        return Ok(entries.Select(ToDto));
    }

    [HttpPost("api/properties/{propertyId}/renovation-entries")]
    public async Task<ActionResult<RenovationEntryDto>> Create(string propertyId, CreateRenovationEntryRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var entry = new RenovationEntry
        {
            PropertyId = propertyId,
            Date = request.Date,
            Category = request.Category,
            Title = request.Title,
            Description = request.Description,
            Amount = request.Amount,
            Vendor = request.Vendor,
            CreatedByUserId = userId,
        };
        db.RenovationEntries.Add(entry);
        await db.SaveChangesAsync();
        return Ok(ToDto(entry));
    }

    [HttpPut("api/renovation-entries/{id}")]
    public async Task<IActionResult> Update(string id, [FromQuery] string propertyId, UpdateRenovationEntryRequest request)
    {
        var entry = await db.RenovationEntries
            .Where(r => r.PropertyId == propertyId && r.Id == id)
            .FirstOrDefaultAsync();
        if (entry is null)
        {
            return NotFound();
        }

        entry.Date = request.Date;
        entry.Category = request.Category;
        entry.Title = request.Title;
        entry.Description = request.Description;
        entry.Amount = request.Amount;
        entry.Vendor = request.Vendor;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("api/renovation-entries/{id}")]
    public async Task<IActionResult> Delete(string id, [FromQuery] string propertyId)
    {
        var entry = await db.RenovationEntries
            .Where(r => r.PropertyId == propertyId && r.Id == id)
            .FirstOrDefaultAsync();
        if (entry is null)
        {
            return NotFound();
        }

        db.RenovationEntries.Remove(entry);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static RenovationEntryDto ToDto(RenovationEntry r) =>
        new(r.Id, r.PropertyId, r.Date, r.Category, r.Title, r.Description, r.Amount, r.Vendor, r.CreatedByUserId, r.CreatedAt);
}
