using HouseApp.Api.Data;
using HouseApp.Api.Dtos.PropertyComponents;
using HouseApp.Api.Extensions;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HouseApp.Api.Controllers;

/// <summary>
/// One property's own component list. The central registry (PropertyComponentsController) is the
/// starting point every property inherits; this is where a household disagrees with it — a component
/// that doesn't apply to their house, an interval that's wrong for their roof, a part nobody central
/// thought of.
///
/// **Not admin-gated, unlike the central registry.** Editing here affects exactly one property and
/// only the people already in it, so the check is the ordinary CanAccessPropertyAsync every
/// per-property controller makes. Admin rights govern the shared registry, not what someone does
/// inside their own house.
/// </summary>
[ApiController]
[Route("api/properties/{propertyId}/components")]
[Authorize]
public class PropertyLocalComponentsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PropertyComponentSetDto>> GetForProperty(string propertyId)
    {
        var property = await LoadAccessibleAsync(propertyId);
        if (property is null)
        {
            return NotFound();
        }

        return Ok(await BuildSetAsync(property));
    }

    [HttpPost]
    public async Task<ActionResult<PropertyLocalComponentDto>> Create(
        string propertyId,
        SavePropertyLocalComponentRequest request)
    {
        var property = await LoadAccessibleAsync(propertyId);
        if (property is null)
        {
            return NotFound();
        }

        await db.EnsureCustomizedAsync(property);
        var component = new PropertyLocalComponent
        {
            Name = request.Name,
            RecommendedIntervalMonths = request.RecommendedIntervalMonths,
        };
        property.LocalComponents!.Add(component);
        await db.SaveChangesAsync();

        // Freshly invented, so it can't match a central id — Local without needing to look.
        return Ok(new PropertyLocalComponentDto(
            component.Id,
            component.Name,
            component.RecommendedIntervalMonths,
            ComponentOrigin.Local,
            CentralName: null,
            CentralIntervalMonths: null));
    }

    [HttpPut("{componentId}")]
    public async Task<IActionResult> Update(
        string propertyId,
        string componentId,
        SavePropertyLocalComponentRequest request)
    {
        var property = await LoadAccessibleAsync(propertyId);
        if (property is null)
        {
            return NotFound();
        }

        // Materialise before looking the component up: until the first edit the property has no list
        // of its own, so the id being edited is a central one that isn't stored here yet.
        await db.EnsureCustomizedAsync(property);
        var component = property.LocalComponents!.SingleOrDefault(c => c.Id == componentId);
        if (component is null)
        {
            return NotFound();
        }

        component.Name = request.Name;
        component.RecommendedIntervalMonths = request.RecommendedIntervalMonths;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{componentId}")]
    public async Task<IActionResult> Delete(string propertyId, string componentId)
    {
        var property = await LoadAccessibleAsync(propertyId);
        if (property is null)
        {
            return NotFound();
        }

        await db.EnsureCustomizedAsync(property);
        var component = property.LocalComponents!.SingleOrDefault(c => c.Id == componentId);
        if (component is null)
        {
            return NotFound();
        }

        // Only this property's projects matter — another household may well have work logged against
        // the same central component, and that's none of this property's business. Single-partition
        // query; projects is partitioned by /propertyId.
        var projects = await db.Projects.Where(p => p.PropertyId == propertyId).ToListAsync();
        if (projects.Any(p => p.ComponentId == componentId))
        {
            return Conflict(new { message = "Component is used by one or more of this property's projects and can't be removed." });
        }

        property.LocalComponents!.Remove(component);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Pulls the central registry back over this property's list: every component that exists in both
    /// is overwritten with the central name and interval, and every central component missing here is
    /// added. Components that exist only here are left alone — they're either this property's own
    /// additions or ones central has since dropped, and in both cases they may have projects logged
    /// against them.
    ///
    /// A no-op on a property that hasn't been customised: it already *is* the central list, and
    /// materialising a copy would only stop it tracking future central changes.
    /// </summary>
    [HttpPost("sync")]
    public async Task<ActionResult<PropertyComponentSetDto>> SyncFromCentral(string propertyId)
    {
        var property = await LoadAccessibleAsync(propertyId);
        if (property is null)
        {
            return NotFound();
        }

        if (property.ComponentsCustomized)
        {
            var local = property.LocalComponents ?? [];
            foreach (var central in await db.PropertyComponents.ToListAsync())
            {
                var existing = local.SingleOrDefault(c => c.Id == central.Id);
                if (existing is null)
                {
                    local.Add(PropertyComponentSet.CopyOf(central));
                }
                else
                {
                    existing.Name = central.Name;
                    existing.RecommendedIntervalMonths = central.RecommendedIntervalMonths;
                }
            }

            property.LocalComponents = local;
            await db.SaveChangesAsync();
        }

        return Ok(await BuildSetAsync(property));
    }

    private async Task<Property?> LoadAccessibleAsync(string propertyId)
    {
        var property = await db.Properties.FindAsync(propertyId);
        if (property is null || !await db.CanAccessPropertyAsync(propertyId, User.CurrentUserId()))
        {
            return null;
        }

        return property;
    }

    private async Task<PropertyComponentSetDto> BuildSetAsync(Property property)
    {
        var central = (await db.PropertyComponents.ToListAsync()).ToDictionary(c => c.Id);
        var components = await db.GetEffectiveComponentsAsync(property);

        var dtos = components
            .Select(c => ToDto(c, central.GetValueOrDefault(c.Id)))
            .OrderBy(c => c.Name)
            .ToList();

        // Nothing is "available to add" while the property still follows central — it has all of it.
        var availableFromCentral = property.ComponentsCustomized
            ? central.Keys.Count(id => components.All(c => c.Id != id))
            : 0;

        return new PropertyComponentSetDto(property.ComponentsCustomized, dtos, availableFromCentral);
    }

    private static PropertyLocalComponentDto ToDto(PropertyLocalComponent local, PropertyComponent? central)
    {
        if (central is null)
        {
            return new PropertyLocalComponentDto(
                local.Id,
                local.Name,
                local.RecommendedIntervalMonths,
                ComponentOrigin.Local,
                CentralName: null,
                CentralIntervalMonths: null);
        }

        var changed = local.Name != central.Name
            || local.RecommendedIntervalMonths != central.RecommendedIntervalMonths;

        return new PropertyLocalComponentDto(
            local.Id,
            local.Name,
            local.RecommendedIntervalMonths,
            changed ? ComponentOrigin.Modified : ComponentOrigin.Central,
            changed ? central.Name : null,
            changed ? central.RecommendedIntervalMonths : null);
    }
}
