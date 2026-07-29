namespace HouseApp.Api.Models;

/// <summary>
/// What you intend to spend on a property in a given year, per kind of work.
///
/// Stores only the budgeted amounts. The original sketch also stored ActualMaintenanceSpent /
/// ActualRenovationSpent / ActualInvestmentSpent — those are deliberately **not** here: the actuals
/// are the sum of ProjectCost rows for the year, so storing them would be wrong the moment a cost
/// row was edited. BudgetsController computes them on read, the same reasoning as Project.ActualCost.
/// </summary>
public class Budget
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    // Partition key — groups all of a property's yearly budgets together.
    public required string PropertyId { get; set; }

    public int Year { get; set; }

    public decimal MaintenanceBudget { get; set; }
    public decimal RenovationBudget { get; set; }
    public decimal InvestmentBudget { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
