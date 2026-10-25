using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Projects;
using HouseApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HouseApp.Api.Services;

/// <summary>
/// Creating and changing projects, shared by the REST API (<c>ProjectsController</c>) and the MCP
/// tools, so the limits, the nested-row caps and the Drive folder rename can't drift apart between
/// the two ways in. **Access is the caller's job** — the REST API allows the demo
/// (<c>CanAccessPropertyAsync</c>), MCP deliberately doesn't — so nothing here checks membership.
///
/// Limits surface as <see cref="LimitReachedException"/>, which each caller turns into its own shape
/// of error: a 409 for the API, a tool error for MCP.
/// </summary>
public class ProjectService(
    AppDbContext db,
    IGoogleDriveService drive,
    IDriveAccessTokenResolver driveTokens,
    ILogger<ProjectService> logger)
{
    public async Task<List<Project>> ListAsync(string propertyId)
    {
        var projects = await db.Projects.Where(p => p.PropertyId == propertyId).ToListAsync();

        // Ordered in memory: a project's date is CompletedDate ?? PlannedStartDate ?? CreatedAt,
        // which isn't a single stored field to sort on server-side.
        return projects.OrderByDescending(SortDate).ToList();
    }

    public async Task<Project?> FindAsync(string propertyId, string id) =>
        await db.Projects.Where(p => p.PropertyId == propertyId && p.Id == id).FirstOrDefaultAsync();

    /// <summary>Saves a new project the caller has filled in.</summary>
    public async Task CreateAsync(Project project)
    {
        EnsureNestedRowsWithinLimits(project);

        // Ids only, single partition — counting without a terminal aggregate, which Cosmos has
        // rejected before (see CLAUDE.md).
        var existing = await db.Projects.Where(p => p.PropertyId == project.PropertyId).Select(p => p.Id).ToListAsync();
        if (existing.Count >= Limits.ProjectsPerProperty)
        {
            throw new LimitReachedException($"En bostad kan ha högst {Limits.ProjectsPerProperty} projekt.");
        }

        db.Projects.Add(project);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Applies <paramref name="change"/> to a loaded project and saves it. The REST API replaces the
    /// whole project; MCP touches only the fields it was given — taking the change as a callback
    /// serves both, and keeps cost and milestone ids stable for a caller that doesn't rewrite them.
    /// </summary>
    public async Task UpdateAsync(Project project, Action<Project> change, CancellationToken cancellationToken = default)
    {
        var nameBefore = project.Name;
        change(project);
        EnsureNestedRowsWithinLimits(project);

        await db.SaveChangesAsync(cancellationToken);

        if (project.Name != nameBefore)
        {
            await RenameDriveFolderAsync(project, cancellationToken);
        }
    }

    /// <summary>Costs and milestones live inside the project document, which Cosmos caps at 2 MB.</summary>
    private static void EnsureNestedRowsWithinLimits(Project project)
    {
        if (project.Costs.Count > Limits.CostsPerProject)
        {
            throw new LimitReachedException($"Ett projekt kan ha högst {Limits.CostsPerProject} kostnadsrader.");
        }

        if (project.Milestones?.Count > Limits.MilestonesPerProject)
        {
            throw new LimitReachedException($"Ett projekt kan ha högst {Limits.MilestonesPerProject} milstolpar.");
        }
    }

    /// <summary>
    /// Keeps the project's Google Drive folder named after the project.
    ///
    /// Deliberately best-effort and *after* the save: a folder name is cosmetic, the files inside it
    /// are already right, and refusing to rename a project because Drive is unreachable would be a
    /// poor trade. A failure leaves the old name until the next rename — visible, not silent, and
    /// harmless. Contrast the document move in DocumentService.SetProjectAsync, which does refuse:
    /// there the file would end up genuinely misfiled.
    /// </summary>
    private async Task RenameDriveFolderAsync(Project project, CancellationToken cancellationToken)
    {
        if (project.GoogleDriveFolderId is not { } folderId)
        {
            return;
        }

        var property = await db.Properties.FindAsync([project.PropertyId], cancellationToken);
        if (property is null || !property.UsesGoogleDrive)
        {
            return;
        }

        try
        {
            var accessToken = await driveTokens.GetForPropertyAsync(property, cancellationToken);
            await drive.RenameFolderAsync(
                accessToken,
                folderId,
                DriveFolderResolver.FolderNameFor(project),
                cancellationToken);
        }
        catch (DriveConnectionExpiredException ex)
        {
            logger.LogWarning(ex, "Could not rename the Drive folder for project {ProjectId}.", project.Id);
        }
    }

    /// <summary>The REST API's whole-project write.</summary>
    public static void Apply(Project project, SaveProjectRequest request)
    {
        project.Name = request.Name;
        project.Description = request.Description;
        project.Notes = request.Notes;
        project.WorkType = request.WorkType;
        project.ComponentId = request.ComponentId;
        project.Status = request.Status;
        project.Priority = request.Priority;
        project.IsUrgent = request.IsUrgent;
        project.ExcludeFromMaintenanceSchedule = request.ExcludeFromMaintenanceSchedule;
        project.PlannedStartDate = request.PlannedStartDate;
        project.ActualStartDate = request.ActualStartDate;
        project.CompletedDate = request.CompletedDate;
        project.EstimatedDurationDays = request.EstimatedDurationDays;
        project.EstimatedCost = request.EstimatedCost;
        project.EstimatedValueIncrease = request.EstimatedValueIncrease;
        project.ExpectedLifespanYears = request.ExpectedLifespanYears;
        project.EnergyEfficiencyGainPercent = request.EnergyEfficiencyGainPercent;

        project.Contractor = request.Contractor is null
            ? null
            : new ContractorInfo
            {
                Name = request.Contractor.Name,
                Phone = request.Contractor.Phone,
                Email = request.Contractor.Email,
                Website = request.Contractor.Website,
                QuotedPrice = request.Contractor.QuotedPrice,
                QuotedDate = request.Contractor.QuotedDate,
                Notes = request.Contractor.Notes,
            };

        // Replaced wholesale rather than merged by id: the costs are nested in this document and
        // nothing outside it references them, so there's no identity worth preserving.
        project.Costs = (request.Costs ?? [])
            .Select(c => new ProjectCost
            {
                Type = c.Type,
                Description = c.Description,
                Amount = c.Amount,
                DateIncurred = c.DateIncurred,
                IsBudgeted = c.IsBudgeted,
            })
            .ToList();

        project.Milestones = (request.Milestones ?? [])
            .Select(m => new ProjectMilestone
            {
                Description = m.Description,
                PlannedDate = m.PlannedDate,
                CompletedDate = m.CompletedDate,
            })
            .ToList();
    }

    private static DateOnly SortDate(Project p) =>
        p.CompletedDate ?? p.PlannedStartDate ?? DateOnly.FromDateTime(p.CreatedAt.UtcDateTime);

    public static ProjectDto ToDto(Project p) =>
        new(
            p.Id,
            p.PropertyId,
            p.Name,
            p.Description,
            p.Notes,
            p.WorkType,
            p.ComponentId,
            p.Status,
            p.Priority,
            p.IsUrgent,
            p.ExcludeFromMaintenanceSchedule,
            p.PlannedStartDate,
            p.ActualStartDate,
            p.CompletedDate,
            p.EstimatedDurationDays,
            p.EstimatedCost,
            p.ActualCost,
            p.EstimatedValueIncrease,
            p.ExpectedLifespanYears,
            p.EnergyEfficiencyGainPercent,
            p.Contractor is null
                ? null
                : new ContractorInfoDto(
                    p.Contractor.Name,
                    p.Contractor.Phone,
                    p.Contractor.Email,
                    p.Contractor.Website,
                    p.Contractor.QuotedPrice,
                    p.Contractor.QuotedDate,
                    p.Contractor.Notes),
            p.Costs
                .Select(c => new ProjectCostDto(c.Id, c.Type, c.Description, c.Amount, c.DateIncurred, c.IsBudgeted))
                .ToList(),
            // ?? [] because projects written before milestones existed have no such property.
            (p.Milestones ?? [])
                .Select(m => new ProjectMilestoneDto(m.Id, m.Description, m.PlannedDate, m.CompletedDate))
                .ToList(),
            p.CreatedByUserId,
            p.CreatedAt);
}
