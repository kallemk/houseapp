using System.Security.Claims;
using HouseApp.Api.Data;
using HouseApp.Api.Extensions;
using HouseApp.Api.Dtos.Projects;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HouseApp.Api.Controllers;

/// <summary>
/// A project is a single Cosmos document with its costs and contractor nested inside, so it is read
/// and written whole — there are no sub-resource endpoints for costs. Update/delete take propertyId
/// as a query-string parameter because it's the partition key, matching the other per-property
/// controllers.
/// </summary>
[ApiController]
[Authorize]
public class ProjectsController(AppDbContext db) : ControllerBase
{
    [HttpGet("api/properties/{propertyId}/projects")]
    public async Task<ActionResult<List<ProjectDto>>> GetForProperty(string propertyId)
    {
        if (!await db.CanAccessPropertyAsync(propertyId, User.CurrentUserId()))
        {
            return NotFound();
        }

        var projects = await db.Projects
            .Where(p => p.PropertyId == propertyId)
            .ToListAsync();

        // Ordered in memory: a project's date is CompletedDate ?? PlannedStartDate ?? CreatedAt,
        // which isn't a single stored field to sort on server-side.
        return Ok(projects.OrderByDescending(SortDate).Select(ToDto));
    }

    [HttpGet("api/projects/{id}")]
    public async Task<ActionResult<ProjectDto>> GetById(string id, [FromQuery] string propertyId)
    {
        if (!await db.CanAccessPropertyAsync(propertyId, User.CurrentUserId()))
        {
            return NotFound();
        }

        var project = await FindAsync(id, propertyId);
        return project is null ? NotFound() : Ok(ToDto(project));
    }

    [HttpPost("api/properties/{propertyId}/projects")]
    public async Task<ActionResult<ProjectDto>> Create(string propertyId, SaveProjectRequest request)
    {
        if (!await db.CanAccessPropertyAsync(propertyId, User.CurrentUserId()))
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var project = new Project
        {
            PropertyId = propertyId,
            Name = request.Name,
            ComponentId = request.ComponentId,
            CreatedByUserId = userId,
        };

        Apply(project, request);
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return Ok(ToDto(project));
    }

    [HttpPut("api/projects/{id}")]
    public async Task<IActionResult> Update(string id, [FromQuery] string propertyId, SaveProjectRequest request)
    {
        if (!await db.CanAccessPropertyAsync(propertyId, User.CurrentUserId()))
        {
            return NotFound();
        }

        var project = await FindAsync(id, propertyId);
        if (project is null)
        {
            return NotFound();
        }

        Apply(project, request);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("api/projects/{id}")]
    public async Task<IActionResult> Delete(string id, [FromQuery] string propertyId)
    {
        if (!await db.CanAccessPropertyAsync(propertyId, User.CurrentUserId()))
        {
            return NotFound();
        }

        var project = await FindAsync(id, propertyId);
        if (project is null)
        {
            return NotFound();
        }

        db.Projects.Remove(project);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<Project?> FindAsync(string id, string propertyId) =>
        await db.Projects.Where(p => p.PropertyId == propertyId && p.Id == id).FirstOrDefaultAsync();

    private static void Apply(Project project, SaveProjectRequest request)
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

    private static ProjectDto ToDto(Project p) =>
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
