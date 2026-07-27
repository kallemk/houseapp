using System.Security.Claims;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Properties;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HouseApp.Api.Controllers;

[ApiController]
[Route("api/properties")]
[Authorize]
public class PropertiesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PropertyDto>>> GetAll()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        // Full container scan + in-memory filter, not a Cosmos query with an array-Contains
        // predicate — the properties container is tiny (a handful of properties, ever), and this
        // sidesteps translating .Contains() on a list property into Cosmos SQL entirely.
        var properties = await db.Properties.ToListAsync();
        var ownProperties = properties.Where(p => IsMember(p, userId));
        return Ok(ownProperties.Select(ToDto));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PropertyDto>> GetById(string id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var property = await db.Properties.FindAsync(id);
        if (property is null || !IsMember(property, userId))
        {
            return NotFound();
        }

        return Ok(ToDto(property));
    }

    [HttpPost]
    public async Task<ActionResult<PropertyDto>> Create(CreatePropertyRequest request)
    {
        // Every account gets connected automatically — there are only ever 2 (admin-seeded), and
        // this app is about a couple sharing visibility into the same house(s), not private
        // per-user properties. No invite/sharing step needed as a result.
        var allUserIds = (await db.Users.ToListAsync()).Select(u => u.Id).ToList();

        var property = new Property
        {
            Nickname = request.Nickname,
            Address = request.Address,
            PurchaseDate = request.PurchaseDate,
            PurchasePrice = request.PurchasePrice,
            MemberUserIds = allUserIds,
        };
        db.Properties.Add(property);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = property.Id }, ToDto(property));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, UpdatePropertyRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var property = await db.Properties.FindAsync(id);
        if (property is null || !IsMember(property, userId))
        {
            return NotFound();
        }

        property.Nickname = request.Nickname;
        property.Address = request.Address;
        property.PurchaseDate = request.PurchaseDate;
        property.PurchasePrice = request.PurchasePrice;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var property = await db.Properties.FindAsync(id);
        if (property is null || !IsMember(property, userId))
        {
            return NotFound();
        }

        db.Properties.Remove(property);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // MemberUserIds is null (not an empty list) for properties that existed before this field was
    // added — a missing JSON property deserializes to the CLR default, not the "= []" initializer
    // — so this must stay null-safe rather than calling .Contains() directly.
    private static bool IsMember(Property property, string userId) =>
        property.MemberUserIds?.Contains(userId) == true;

    private static PropertyDto ToDto(Property p) =>
        new(p.Id, p.Nickname, p.Address, p.PurchaseDate, p.PurchasePrice, p.CreatedAt);
}
