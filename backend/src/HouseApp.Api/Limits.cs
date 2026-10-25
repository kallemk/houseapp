using Microsoft.AspNetCore.Mvc;

namespace HouseApp.Api;

/// <summary>
/// Ceilings on what one account or property can hold. Registration is open and Cosmos is
/// serverless — every stored row and every request is paid for — so nothing a user can create is
/// left unbounded. The numbers are generous for a real household and only meant to stop a script
/// or a runaway client.
///
/// A count limit answers 409 with <c>code: "limit_reached"</c> and a Swedish message the UI shows
/// as-is (<see cref="Reached"/>). Text lengths are <c>[MaxLength]</c> on the request DTOs, which
/// <c>[ApiController]</c> turns into a 400 by itself.
/// </summary>
public static class Limits
{
    /// <summary>Per user, counting memberships — there's no owner role to count instead. The demo doesn't count.</summary>
    public const int PropertiesPerUser = 5;

    public const int MembersPerProperty = 10;
    public const int ProjectsPerProperty = 200;
    public const int BudgetYearsPerProperty = 50;
    public const int ValuationsPerProperty = 300;
    public const int DocumentsPerProperty = 1000;
    public const int LocalComponentsPerProperty = 100;

    /// <summary>Costs and milestones are nested in the project document, which Cosmos caps at 2 MB.</summary>
    public const int CostsPerProject = 200;
    public const int MilestonesPerProject = 50;

    /// <summary>Blob only — a Drive property's files sit in the owner's own Drive, not on our bill.</summary>
    public const long BlobStorageBytesPerProperty = 2L * 1024 * 1024 * 1024;

    /// <summary>Per file, for both storage backends.</summary>
    public const long MaxFileBytes = 25 * 1024 * 1024;

    public const int MinBudgetYear = 1900;
    public const int MaxBudgetYear = 2100;

    /// <summary>Names, titles, addresses and other one-line fields.</summary>
    public const int ShortText = 200;

    /// <summary>Descriptions, notes and other free text.</summary>
    public const int LongText = 4000;

    public const string LimitReachedCode = "limit_reached";

    public static ConflictObjectResult Reached(string message) =>
        new(new { code = LimitReachedCode, message });
}

/// <summary>
/// A limit thrown from a service shared by the REST API and MCP, so each can answer in its own
/// shape — <see cref="Limits.Reached"/> for HTTP, a tool error for MCP. The message is Swedish and
/// meant to be shown as-is.
/// </summary>
public class LimitReachedException(string message) : Exception(message);
