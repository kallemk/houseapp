using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Maintenance;
using HouseApp.Api.Extensions;
using HouseApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HouseApp.Api.Controllers;

/// <summary>
/// When each part of the house is next due for maintenance — computed by
/// <see cref="MaintenanceScheduleCalculator"/>, which the MCP tools share.
/// </summary>
[ApiController]
[Authorize]
public class MaintenanceScheduleController(AppDbContext db) : ControllerBase
{
    [HttpGet("api/properties/{propertyId}/maintenance-schedule")]
    public async Task<ActionResult<List<MaintenanceScheduleItemDto>>> GetForProperty(
        string propertyId,
        [FromQuery] DateOnly? asOf = null)
    {
        var today = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);

        // Access check first: this reads the property's projects, so it must not answer for a
        // property the caller has nothing to do with.
        if (!await db.CanAccessPropertyAsync(propertyId, User.CurrentUserId()))
        {
            return NotFound();
        }

        var property = await db.Properties.FindAsync(propertyId);
        if (property is null)
        {
            return NotFound();
        }

        return Ok(await MaintenanceScheduleCalculator.ComputeAsync(db, property, today));
    }
}
