namespace HouseApp.Api.Models;

/// <summary>
/// One property's own copy of a component. Owned by Property — nested JSON, no container of its own,
/// for the same reason ProjectCost is: a property's components are never read without the property,
/// and there are perhaps a dozen of them.
///
/// **Id is copied verbatim from the central PropertyComponent it came from, not regenerated.** That
/// is what makes the whole feature free of migration: Project.ComponentId already holds central ids,
/// and every one of them keeps resolving once a property has its own set. A component invented here
/// gets a fresh Guid, which will never collide with a central one. It also means "does the central
/// registry still have this id?" is the complete answer to where a row came from — no snapshot of the
/// central values needs storing to work out what's been changed locally.
/// </summary>
public class PropertyLocalComponent
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string Name { get; set; }
    public int? RecommendedIntervalMonths { get; set; }
}
