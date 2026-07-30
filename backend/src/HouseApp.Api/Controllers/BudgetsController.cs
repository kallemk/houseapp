using HouseApp.Api.Data;
using HouseApp.Api.Extensions;
using HouseApp.Api.Dtos.Budgets;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HouseApp.Api.Controllers;

/// <summary>
/// Yearly spending plan per property. Only the budgeted amounts are stored; what's actually been
/// spent is summed from ProjectCost rows on every read.
///
/// A cost belongs to the year of its own DateIncurred, not the project's completion date, so a job
/// running over New Year splits across both years the way the money actually did.
/// </summary>
[ApiController]
[Authorize]
public class BudgetsController(AppDbContext db) : ControllerBase
{
    [HttpGet("api/properties/{propertyId}/budgets/{year:int}")]
    public async Task<ActionResult<BudgetDto>> GetForYear(string propertyId, int year)
    {
        if (!await db.CanAccessPropertyAsync(propertyId, User.CurrentUserId()))
        {
            return NotFound();
        }

        var budget = (await db.Budgets.Where(b => b.PropertyId == propertyId).ToListAsync())
            .FirstOrDefault(b => b.Year == year);
        var projects = await db.Projects.Where(p => p.PropertyId == propertyId).ToListAsync();

        return Ok(BuildDto(propertyId, year, budget, projects));
    }

    /// <summary>
    /// Every year that has either a saved budget or any recorded spend, newest first — so a year
    /// where money was spent without a plan still shows up.
    /// </summary>
    [HttpGet("api/properties/{propertyId}/budgets")]
    public async Task<ActionResult<List<BudgetDto>>> GetAll(string propertyId)
    {
        if (!await db.CanAccessPropertyAsync(propertyId, User.CurrentUserId()))
        {
            return NotFound();
        }

        var budgets = await db.Budgets.Where(b => b.PropertyId == propertyId).ToListAsync();
        var projects = await db.Projects.Where(p => p.PropertyId == propertyId).ToListAsync();

        var years = budgets.Select(b => b.Year)
            .Concat(projects.SelectMany(p => p.Costs).Select(c => c.DateIncurred.Year))
            .Distinct()
            .OrderByDescending(y => y)
            .ToList();

        var dtos = years
            .Select(year => BuildDto(propertyId, year, budgets.FirstOrDefault(b => b.Year == year), projects))
            .ToList();

        return Ok(dtos);
    }

    /// <summary>Upsert: one budget per property per year, so saving the same year again edits it.</summary>
    [HttpPut("api/properties/{propertyId}/budgets/{year:int}")]
    public async Task<ActionResult<BudgetDto>> Save(string propertyId, int year, SaveBudgetRequest request)
    {
        if (!await db.CanAccessPropertyAsync(propertyId, User.CurrentUserId()))
        {
            return NotFound();
        }

        if (request.Year != year)
        {
            return BadRequest(new { message = "Year in the URL and body must match." });
        }

        var budget = (await db.Budgets.Where(b => b.PropertyId == propertyId).ToListAsync())
            .FirstOrDefault(b => b.Year == year);

        if (budget is null)
        {
            budget = new Budget { PropertyId = propertyId, Year = year };
            db.Budgets.Add(budget);
        }

        budget.MaintenanceBudget = request.MaintenanceBudget;
        budget.RenovationBudget = request.RenovationBudget;
        budget.InvestmentBudget = request.InvestmentBudget;
        await db.SaveChangesAsync();

        var projects = await db.Projects.Where(p => p.PropertyId == propertyId).ToListAsync();
        return Ok(BuildDto(propertyId, year, budget, projects));
    }

    [HttpDelete("api/properties/{propertyId}/budgets/{year:int}")]
    public async Task<IActionResult> Delete(string propertyId, int year)
    {
        if (!await db.CanAccessPropertyAsync(propertyId, User.CurrentUserId()))
        {
            return NotFound();
        }

        var budget = (await db.Budgets.Where(b => b.PropertyId == propertyId).ToListAsync())
            .FirstOrDefault(b => b.Year == year);
        if (budget is null)
        {
            return NotFound();
        }

        db.Budgets.Remove(budget);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static BudgetDto BuildDto(string propertyId, int year, Budget? budget, List<Project> projects)
    {
        var lines = new List<BudgetLineDto>
        {
            Line(WorkType.Maintenance, budget?.MaintenanceBudget ?? 0m, projects, year),
            Line(WorkType.Renovation, budget?.RenovationBudget ?? 0m, projects, year),
            Line(WorkType.Investment, budget?.InvestmentBudget ?? 0m, projects, year),
        };

        return new BudgetDto(
            budget?.Id,
            propertyId,
            year,
            lines,
            lines.Sum(l => l.Budgeted),
            lines.Sum(l => l.Spent));
    }

    private static BudgetLineDto Line(WorkType workType, decimal budgeted, List<Project> projects, int year)
    {
        var spent = projects
            .Where(p => p.WorkType == workType)
            .SelectMany(p => p.Costs)
            .Where(c => c.DateIncurred.Year == year)
            .Sum(c => c.Amount);

        return new BudgetLineDto(workType, budgeted, spent);
    }
}
