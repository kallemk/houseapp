using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Maintenance;
using HouseApp.Api.Extensions;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HouseApp.Api.Controllers;

/// <summary>
/// When each part of the house is next due for maintenance. Read-only and fully derived: there is no
/// maintenanceSchedule container, because every field would be a copy of something already stored —
/// the interval lives on PropertyComponent, and "last completed" is the newest completed Maintenance
/// or Renovation project for that component. A stored copy would go stale the moment a project's
/// date was edited.
/// </summary>
[ApiController]
[Authorize]
public class MaintenanceScheduleController(AppDbContext db) : ControllerBase
{
    /// <summary>How close to the due date counts as "due soon" rather than "ok".</summary>
    private const int DueSoonWindowMonths = 3;

    /// <summary>
    /// Work that resets the clock on a component. Renovation counts alongside Maintenance because
    /// replacing or renovating a part extends its life at least as much as servicing it — a roof
    /// redone last year isn't due for maintenance just because it was logged as a renovation.
    /// Investment does not: it's new capital work, not upkeep of the existing part.
    /// </summary>
    private static readonly WorkType[] LifeExtendingWork = [WorkType.Maintenance, WorkType.Renovation];

    [HttpGet("api/properties/{propertyId}/maintenance-schedule")]
    public async Task<ActionResult<List<MaintenanceScheduleItemDto>>> GetForProperty(
        string propertyId,
        [FromQuery] DateOnly? asOf = null)
    {
        var today = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);

        // Access check first: this reads the property's projects, so it must not answer for a
        // property the caller has nothing to do with.
        if (!await db.CanAccessPropertyAsync(propertyId, User.CurrentUserId()))
        {
            return NotFound();
        }

        var property = await db.Properties.FindAsync(propertyId);
        if (property is null)
        {
            return NotFound();
        }

        var components = await db.PropertyComponents.ToListAsync();
        var projects = await db.Projects.Where(p => p.PropertyId == propertyId).ToListAsync();

        var items = components
            .Select(component => Build(component, projects, property.YearBuilt, today))
            // Most urgent first, then by how soon it's due; unschedulable components sink to the
            // bottom rather than cluttering the top of the list.
            .OrderByDescending(i => i.Urgency)
            .ThenBy(i => i.NextDueDate ?? DateOnly.MaxValue)
            .ThenBy(i => i.ComponentName)
            .ToList();

        return Ok(items);
    }

    private static MaintenanceScheduleItemDto Build(
        PropertyComponent component,
        List<Project> projects,
        int? yearBuilt,
        DateOnly today)
    {
        // Projects tagged as minor are dropped up front: patching a few tiles shouldn't push the
        // next roof service out by the whole interval, and it shouldn't count as one being planned
        // either. Deliberately applied here rather than only to `lastCompleted`, so a minor job is
        // invisible to the schedule in both directions.
        var forComponent = projects
            .Where(p => p.ComponentId == component.Id && !p.ExcludeFromMaintenanceSchedule)
            .ToList();

        var lastCompleted = forComponent
            .Where(p => LifeExtendingWork.Contains(p.WorkType) && p.Status == ProjectStatus.Completed && p.CompletedDate is not null)
            .OrderByDescending(p => p.CompletedDate)
            .FirstOrDefault();

        var hasUpcoming = forComponent.Any(p =>
            LifeExtendingWork.Contains(p.WorkType) &&
            p.Status is ProjectStatus.Planned or ProjectStatus.InProgress);

        // With nothing logged, the house's build year stands in: the component is assumed to date
        // from when the house was built. It's a starting point to correct by logging real work, not
        // a claim that anything was done — which is why the baseline is reported alongside it.
        var (baselineDate, baseline) = lastCompleted?.CompletedDate is { } completedDate
            ? (completedDate, MaintenanceBaseline.Project)
            : yearBuilt is { } year
                ? (new DateOnly(year, 1, 1), MaintenanceBaseline.YearBuilt)
                : ((DateOnly?)null, MaintenanceBaseline.None);

        var interval = component.RecommendedIntervalMonths;
        if (interval is null or <= 0)
        {
            return new MaintenanceScheduleItemDto(
                component.Id,
                component.Name,
                component.RecommendedIntervalMonths,
                baselineDate,
                baseline,
                lastCompleted?.Id,
                lastCompleted?.Name,
                NextDueDate: null,
                MonthsUntilDue: null,
                MaintenanceUrgency.NotScheduled,
                hasUpcoming);
        }

        if (baselineDate is not { } anchor)
        {
            // An interval is known but there's nothing at all to count from — no logged work and no
            // build year. Deliberately not treated as overdue: that would be a guess stated as fact.
            return new MaintenanceScheduleItemDto(
                component.Id,
                component.Name,
                interval,
                LastCompletedDate: null,
                MaintenanceBaseline.None,
                LastProjectId: null,
                LastProjectName: null,
                NextDueDate: null,
                MonthsUntilDue: null,
                MaintenanceUrgency.Unknown,
                hasUpcoming);
        }

        var nextDue = anchor.AddMonths(interval.Value);
        var monthsUntilDue = MonthsBetween(today, nextDue);

        var urgency = nextDue <= today
            ? MaintenanceUrgency.Overdue
            : monthsUntilDue <= DueSoonWindowMonths
                ? MaintenanceUrgency.DueSoon
                : MaintenanceUrgency.Ok;

        return new MaintenanceScheduleItemDto(
            component.Id,
            component.Name,
            interval,
            anchor,
            baseline,
            lastCompleted?.Id,
            lastCompleted?.Name,
            nextDue,
            monthsUntilDue,
            urgency,
            hasUpcoming);
    }

    /// <summary>Whole months from <paramref name="from"/> to <paramref name="to"/>, negative when past due.</summary>
    private static int MonthsBetween(DateOnly from, DateOnly to)
    {
        var months = ((to.Year - from.Year) * 12) + to.Month - from.Month;
        // Don't count a month that hasn't fully elapsed: 31 Jan → 1 Feb is 0 months, not 1.
        if (to.Day < from.Day)
        {
            months--;
        }

        return months;
    }
}
