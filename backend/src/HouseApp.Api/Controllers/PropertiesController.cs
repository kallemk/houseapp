using HouseApp.Api.Authorization;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Properties;
using HouseApp.Api.Extensions;
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
        var userId = User.CurrentUserId();

        // Full container scan + in-memory filter, not a Cosmos query with an array-Contains
        // predicate — the properties container is tiny (a handful of properties, ever), and this
        // sidesteps translating .Contains() on a list property into Cosmos SQL entirely.
        var properties = await db.Properties.ToListAsync();
        var visible = properties.Where(p => p.IsDemo || PropertyAccess.IsMember(p, userId));
        return Ok(visible.Select(p => ToDto(p, userId)));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PropertyDto>> GetById(string id)
    {
        var userId = User.CurrentUserId();
        var property = await db.Properties.FindAsync(id);
        if (property is null || !(property.IsDemo || PropertyAccess.IsMember(property, userId)))
        {
            return NotFound();
        }

        return Ok(ToDto(property, userId));
    }

    [HttpPost]
    public async Task<ActionResult<PropertyDto>> Create(SavePropertyRequest request)
    {
        // Only the creator. Everyone else gets in by being added on the access modal — see the
        // member endpoints below. This used to stamp every account in the database, which was fine
        // for two people sharing one house and became a data leak the moment a third household
        // joined.
        var userId = User.CurrentUserId();

        var property = new Property
        {
            Nickname = request.Nickname,
            Address = request.Address,
            MemberUserIds = [userId],
        };
        Apply(property, request);
        db.Properties.Add(property);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = property.Id }, ToDto(property, userId));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, SavePropertyRequest request)
    {
        var property = await db.Properties.FindAsync(id);
        if (property is null || !await db.CanAccessPropertyAsync(id, User.CurrentUserId()))
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
        // Strict membership, not demo access: the demo is a sandbox anyone may edit, but nobody
        // outside it may delete it out from under everyone.
        var property = await db.Properties.FindAsync(id);
        if (property is null || !await db.IsPropertyMemberAsync(id, User.CurrentUserId()))
        {
            return NotFound();
        }

        if (property.IsDemo)
        {
            return Conflict(new { message = "The demo property can't be deleted. Remove the demo flag first." });
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

    // --- Access management ------------------------------------------------------------------
    // All of these require *strict* membership: demo access lets you play in the sandbox, not decide
    // who else gets in.

    [HttpGet("{id}/members")]
    public async Task<ActionResult<List<PropertyMemberDto>>> GetMembers(string id)
    {
        var property = await db.Properties.FindAsync(id);
        if (property is null || !PropertyAccess.IsMember(property, User.CurrentUserId()))
        {
            return NotFound();
        }

        var memberIds = property.MemberUserIds ?? [];
        var users = await db.Users.ToListAsync();
        return Ok(users
            .Where(u => memberIds.Contains(u.Id))
            .OrderBy(u => u.DisplayName)
            .Select(u => new PropertyMemberDto(u.Id, u.Email, u.DisplayName)));
    }

    /// <summary>
    /// People who could be added, matched on a fragment of their name or email. Scoped to a property
    /// you're already in rather than being a global user search, capped, and silent below two
    /// characters — enough to find someone you know of, not enough to enumerate the directory. It
    /// searches the sign-in allowlist; it does not extend it (that's still the admin users page).
    /// </summary>
    [HttpGet("{id}/member-candidates")]
    // Nullable: an absent or empty query is a normal "user hasn't typed enough yet", not a bad
    // request — [ApiController] would otherwise reject ?query= with a 400 before reaching this.
    public async Task<ActionResult<List<PropertyMemberDto>>> SearchMemberCandidates(string id, [FromQuery] string? query)
    {
        var property = await db.Properties.FindAsync(id);
        if (property is null || !PropertyAccess.IsMember(property, User.CurrentUserId()))
        {
            return NotFound();
        }

        var trimmed = (query ?? string.Empty).Trim();
        if (trimmed.Length < 2)
        {
            return Ok(new List<PropertyMemberDto>());
        }

        var memberIds = property.MemberUserIds ?? [];
        var users = await db.Users.ToListAsync();
        return Ok(users
            .Where(u => !memberIds.Contains(u.Id))
            .Where(u =>
                u.DisplayName.Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                u.Email.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
            .OrderBy(u => u.DisplayName)
            .Take(10)
            .Select(u => new PropertyMemberDto(u.Id, u.Email, u.DisplayName)));
    }

    [HttpPost("{id}/members")]
    public async Task<IActionResult> AddMember(string id, AddPropertyMemberRequest request)
    {
        var property = await db.Properties.FindAsync(id);
        if (property is null || !PropertyAccess.IsMember(property, User.CurrentUserId()))
        {
            return NotFound();
        }

        var user = await db.Users.FindAsync(request.UserId);
        if (user is null)
        {
            return NotFound(new { message = "No such user." });
        }

        property.MemberUserIds ??= [];
        if (property.MemberUserIds.Contains(user.Id))
        {
            return Conflict(new { message = "That person already has access." });
        }

        property.MemberUserIds.Add(user.Id);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}/members/{userId}")]
    public async Task<IActionResult> RemoveMember(string id, string userId)
    {
        var property = await db.Properties.FindAsync(id);
        if (property is null || !PropertyAccess.IsMember(property, User.CurrentUserId()))
        {
            return NotFound();
        }

        var memberIds = property.MemberUserIds ?? [];
        if (!memberIds.Contains(userId))
        {
            return NotFound();
        }

        // Nobody left would mean nobody can ever get back in: admins deliberately don't bypass
        // membership, so an empty list orphans the property and its data for good.
        if (memberIds.Count == 1)
        {
            return Conflict(new { message = "A property must keep at least one member." });
        }

        memberIds.Remove(userId);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Admin-only, and deliberately not part of SavePropertyRequest: marking a property as the demo
    /// exposes it to every account, so it must not be something a member can do while editing their
    /// own house. Setting it clears the flag everywhere else — there is only ever one demo.
    /// </summary>
    [HttpPut("{id}/demo")]
    [Authorize(Policy = Policies.Admin)]
    public async Task<IActionResult> SetDemo(string id, SetDemoPropertyRequest request)
    {
        var property = await db.Properties.FindAsync(id);
        if (property is null)
        {
            return NotFound();
        }

        if (request.IsDemo)
        {
            foreach (var other in (await db.Properties.ToListAsync()).Where(p => p.IsDemo && p.Id != id))
            {
                other.IsDemo = false;
            }
        }

        property.IsDemo = request.IsDemo;
        await db.SaveChangesAsync();
        return NoContent();
    }

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

    private static PropertyDto ToDto(Property p, string userId) =>
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
            p.IsDemo,
            PropertyAccess.IsMember(p, userId),
            p.CreatedAt);
}
