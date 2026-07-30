using HouseApp.Api.Models;

namespace HouseApp.Api.Dtos.Projects;

public record ProjectCostDto(
    string Id,
    CostType Type,
    string? Description,
    decimal Amount,
    DateOnly DateIncurred,
    bool IsBudgeted);

public record ProjectMilestoneDto(
    string Id,
    string Description,
    DateOnly? PlannedDate,
    DateOnly? CompletedDate);

public record ContractorInfoDto(
    string Name,
    string? Phone,
    string? Email,
    string? Website,
    decimal? QuotedPrice,
    DateOnly? QuotedDate,
    string? Notes);

/// <summary>ActualCost is derived from Costs — returned for convenience, never accepted on write.</summary>
public record ProjectDto(
    string Id,
    string PropertyId,
    string Name,
    string? Description,
    string? Notes,
    WorkType WorkType,
    string ComponentId,
    ProjectStatus Status,
    ProjectPriority Priority,
    bool IsUrgent,
    /// <summary>True for work too minor to reset the component's maintenance clock.</summary>
    bool ExcludeFromMaintenanceSchedule,
    DateOnly? PlannedStartDate,
    DateOnly? ActualStartDate,
    DateOnly? CompletedDate,
    int? EstimatedDurationDays,
    decimal EstimatedCost,
    decimal ActualCost,
    decimal? EstimatedValueIncrease,
    int? ExpectedLifespanYears,
    decimal? EnergyEfficiencyGainPercent,
    ContractorInfoDto? Contractor,
    List<ProjectCostDto> Costs,
    List<ProjectMilestoneDto> Milestones,
    string CreatedByUserId,
    DateTimeOffset CreatedAt);

public record ProjectMilestoneRequest(
    string Description,
    DateOnly? PlannedDate,
    DateOnly? CompletedDate);

/// <summary>Costs carry no id — they're nested in the project document and nothing references them individually.</summary>
public record ProjectCostRequest(
    CostType Type,
    string? Description,
    decimal Amount,
    DateOnly DateIncurred,
    bool IsBudgeted);

/// <summary>
/// Used for both create and update: a project is a single Cosmos document, so it's written whole.
/// That includes its costs and contractor — there are deliberately no sub-resource endpoints.
/// </summary>
public record SaveProjectRequest(
    string Name,
    string? Description,
    string? Notes,
    WorkType WorkType,
    string ComponentId,
    ProjectStatus Status,
    ProjectPriority Priority,
    bool IsUrgent,
    /// <summary>True for work too minor to reset the component's maintenance clock.</summary>
    bool ExcludeFromMaintenanceSchedule,
    DateOnly? PlannedStartDate,
    DateOnly? ActualStartDate,
    DateOnly? CompletedDate,
    int? EstimatedDurationDays,
    decimal EstimatedCost,
    decimal? EstimatedValueIncrease,
    int? ExpectedLifespanYears,
    decimal? EnergyEfficiencyGainPercent,
    ContractorInfoDto? Contractor,
    List<ProjectCostRequest>? Costs,
    List<ProjectMilestoneRequest>? Milestones);
