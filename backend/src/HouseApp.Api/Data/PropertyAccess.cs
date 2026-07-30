using HouseApp.Api.Models;

namespace HouseApp.Api.Data;

/// <summary>
/// The single place that decides who may touch a property's data.
///
/// Every controller that takes a propertyId — projects, valuations, documents, budgets, the
/// maintenance schedule — must call one of these before doing anything with it. They accept that id
/// from a route, query string or request body, so without a check any signed-in user could read or
/// write another household's data just by supplying the id, which every member has sitting in their
/// URL bar. **A new per-property controller needs the same call.**
/// </summary>
public static class PropertyAccess
{
    /// <summary>
    /// May this user see and change the property's contents? True for members, and for everyone on
    /// the demo property — that one is a shared sandbox, so read and write are the same question.
    /// </summary>
    public static async Task<bool> CanAccessPropertyAsync(this AppDbContext db, string propertyId, string userId)
    {
        var property = await db.Properties.FindAsync(propertyId);
        return property is not null && (property.IsDemo || IsMember(property, userId));
    }

    /// <summary>
    /// Strict membership — demo access is not enough. For the things that must stay with the people
    /// who actually own the property: managing who else has access, and deleting it.
    /// </summary>
    public static async Task<bool> IsPropertyMemberAsync(this AppDbContext db, string propertyId, string userId)
    {
        var property = await db.Properties.FindAsync(propertyId);
        return property is not null && IsMember(property, userId);
    }

    /// <summary>
    /// MemberUserIds is null (not an empty list) on properties that predate the field — a missing
    /// JSON property becomes the CLR default, not the "= []" initializer. Never call .Contains()
    /// on it directly; this once crashed GetAll in production.
    /// </summary>
    public static bool IsMember(Property property, string userId) =>
        property.MemberUserIds?.Contains(userId) == true;
}
