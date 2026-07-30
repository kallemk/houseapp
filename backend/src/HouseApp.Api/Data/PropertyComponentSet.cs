using HouseApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HouseApp.Api.Data;

/// <summary>
/// The single place that answers "which components does this property actually have?".
///
/// A property either follows the central registry or has its own list — see
/// <see cref="Property.ComponentsCustomized"/>. Nothing outside this class should branch on that
/// flag: read the effective list through <see cref="GetEffectiveComponentsAsync"/> and the two cases
/// look identical. The maintenance schedule, the project component dropdown and the property's own
/// component page all go through here.
/// </summary>
public static class PropertyComponentSet
{
    /// <summary>
    /// The components in force for this property: its own list once customised, otherwise a live
    /// view of the central registry. The uncustomised case is not persisted — a property that has
    /// never been touched keeps tracking central additions and edits for free, which is what makes
    /// central administration worth having.
    /// </summary>
    public static async Task<List<PropertyLocalComponent>> GetEffectiveComponentsAsync(
        this AppDbContext db,
        Property property)
    {
        if (property.ComponentsCustomized)
        {
            return property.LocalComponents ?? [];
        }

        var central = await db.PropertyComponents.ToListAsync();
        return central.Select(CopyOf).ToList();
    }

    /// <summary>
    /// Materialises the central registry into this property's own list, if it hasn't been already.
    /// Called before any local edit, so the first change to one component doesn't silently drop the
    /// rest. Does not save — the caller's SaveChangesAsync covers it.
    /// </summary>
    public static async Task EnsureCustomizedAsync(this AppDbContext db, Property property)
    {
        if (property.ComponentsCustomized)
        {
            property.LocalComponents ??= [];
            return;
        }

        var central = await db.PropertyComponents.ToListAsync();
        property.LocalComponents = central.Select(CopyOf).ToList();
        property.ComponentsCustomized = true;
    }

    /// <summary>
    /// Copies a central component keeping its id — see PropertyLocalComponent for why that matters.
    /// </summary>
    public static PropertyLocalComponent CopyOf(PropertyComponent central) => new()
    {
        Id = central.Id,
        Name = central.Name,
        RecommendedIntervalMonths = central.RecommendedIntervalMonths,
    };
}
