using System.Security.Claims;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Properties;
using HouseApp.Api.Models;
using HouseApp.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HouseApp.Api.Controllers;

[ApiController]
[Route("api/properties")]
[Authorize]
public class PropertiesController(AppDbContext db, IBlobStorageService blobStorage) : ControllerBase
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
    public async Task<ActionResult<PropertyDto>> Create(SavePropertyRequest request)
    {
        // Every account gets connected automatically — there are only ever 2 (admin-seeded), and
        // this app is about a couple sharing visibility into the same house(s), not private
        // per-user properties. No invite/sharing step needed as a result.
        var allUserIds = (await db.Users.ToListAsync()).Select(u => u.Id).ToList();

        var property = new Property
        {
            Nickname = request.Nickname,
            Address = request.Address,
            MemberUserIds = allUserIds,
        };
        Apply(property, request);
        db.Properties.Add(property);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = property.Id }, ToDto(property));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, SavePropertyRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var property = await db.Properties.FindAsync(id);
        if (property is null || !IsMember(property, userId))
        {
            return NotFound();
        }

        Apply(property, request);
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

        // Cosmos has no cascade delete and no cross-container foreign keys, so everything hanging
        // off this property has to be removed explicitly. Skipping it wouldn't just waste storage:
        // the orphaned entries stay in their containers permanently and unreachable, since the UI
        // only ever reaches them through a property that no longer exists. All three of these are
        // single-partition queries — those containers are partitioned by /propertyId.
        var valuations = await db.ValuationEntries.Where(v => v.PropertyId == id).ToListAsync();
        var projects = await db.Projects.Where(p => p.PropertyId == id).ToListAsync();
        var documents = await db.Documents.Where(d => d.PropertyId == id).ToListAsync();
        var budgets = await db.Budgets.Where(b => b.PropertyId == id).ToListAsync();

        // Blobs live outside Cosmos entirely — nothing else would ever clean them up. Deleted
        // before the rows so a failure here leaves the (still-reachable) documents intact rather
        // than dropping the only pointer to a blob that then leaks silently.
        foreach (var document in documents)
        {
            await blobStorage.DeleteAsync(document.BlobPath);
        }

        // The legacy renovationEntries container is deliberately not cascaded — it's a frozen
        // backup of the pre-project model, kept as the rollback path.
        db.ValuationEntries.RemoveRange(valuations);
        db.Projects.RemoveRange(projects);
        db.Documents.RemoveRange(documents);
        db.Budgets.RemoveRange(budgets);
        db.Properties.Remove(property);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // MemberUserIds is null (not an empty list) for properties that existed before this field was
    // added — a missing JSON property deserializes to the CLR default, not the "= []" initializer
    // — so this must stay null-safe rather than calling .Contains() directly.
    private static bool IsMember(Property property, string userId) =>
        property.MemberUserIds?.Contains(userId) == true;

    private static void Apply(Property property, SavePropertyRequest request)
    {
        property.Nickname = request.Nickname;
        property.Address = request.Address;
        property.Address2 = request.Address2;
        property.PostalCode = request.PostalCode;
        property.City = request.City;
        property.Country = request.Country;
        property.PropertyDesignation = request.PropertyDesignation;
        property.YearBuilt = request.YearBuilt;
        property.Type = request.Type;
        property.Latitude = request.Latitude;
        property.Longitude = request.Longitude;
        property.PurchaseDate = request.PurchaseDate;
        property.PurchasePrice = request.PurchasePrice;
    }

    private static PropertyDto ToDto(Property p) =>
        new(
            p.Id,
            p.Nickname,
            p.Address,
            p.Address2,
            p.PostalCode,
            p.City,
            p.Country,
            p.PropertyDesignation,
            p.YearBuilt,
            p.Type,
            p.Latitude,
            p.Longitude,
            p.PurchaseDate,
            p.PurchasePrice,
            p.CreatedAt);
}
