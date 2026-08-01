using HouseApp.Api.Models;

namespace HouseApp.Api.Dtos.Budgets;

/// <summary>One row of the year: what was planned, what's been spent, and the difference.</summary>
public record BudgetLineDto(WorkType WorkType, decimal Budgeted, decimal Spent)
{
    /// <summary>Positive means money left, negative means over budget.</summary>
    public decimal Remaining => Budgeted - Spent;
}

/// <summary>
/// Spent is always computed from ProjectCost rows and never stored — see the note on Budget.
/// A year with no saved budget still returns a row (all budgeted amounts zero) so spend without a
/// plan is visible rather than hidden.
/// </summary>
public record BudgetDto(
    string? Id,
    string PropertyId,
    int Year,
    List<BudgetLineDto> Lines,
    decimal TotalBudgeted,
    decimal TotalSpent);

public record SaveBudgetRequest(
    int Year,
    decimal MaintenanceBudget,
    decimal RenovationBudget,
    decimal InvestmentBudget,
    decimal PurchaseBudget);
