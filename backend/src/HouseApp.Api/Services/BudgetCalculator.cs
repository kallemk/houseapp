using HouseApp.Api.Dtos.Budgets;
using HouseApp.Api.Models;

namespace HouseApp.Api.Services;

/// <summary>
/// A year's budget against what was actually spent, per work type. Only the budgeted amounts are
/// stored; spend is summed from cost rows on read, by each cost's own DateIncurred. Shared by
/// <c>BudgetsController</c> and the MCP tools.
/// </summary>
public static class BudgetCalculator
{
    public static BudgetDto Build(string propertyId, int year, Budget? budget, List<Project> projects)
    {
        var lines = new List<BudgetLineDto>
        {
            Line(WorkType.Maintenance, budget?.MaintenanceBudget ?? 0m, projects, year),
            Line(WorkType.Renovation, budget?.RenovationBudget ?? 0m, projects, year),
            Line(WorkType.Investment, budget?.InvestmentBudget ?? 0m, projects, year),
            Line(WorkType.Purchase, budget?.PurchaseBudget ?? 0m, projects, year),
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
