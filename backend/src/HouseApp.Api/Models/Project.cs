namespace HouseApp.Api.Models;

/// <summary>
/// A piece of work on the house — planned, ongoing or finished. Replaces the old RenovationEntry,
/// which could only express "on this date we spent this much".
///
/// Costs and Contractor are EF owned types, stored as nested JSON inside this one document rather
/// than in containers of their own: they are never read without their project, so nesting means one
/// read per project and no cross-container joins (which the Cosmos provider doesn't do anyway).
/// </summary>
public class Project
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    // Partition key — groups all projects for a property together.
    public required string PropertyId { get; set; }

    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }

    public WorkType WorkType { get; set; }

    /// <summary>
    /// Which part of the house this concerns — references PropertyComponent.Id. Admin-managed data
    /// rather than an enum, so the list can be edited in-app (same shape as the old RenovationType).
    /// </summary>
    public required string ComponentId { get; set; }

    public ProjectStatus Status { get; set; }
    public ProjectPriority Priority { get; set; }
    public bool IsUrgent { get; set; }

    public DateOnly? PlannedStartDate { get; set; }
    public DateOnly? ActualStartDate { get; set; }
    public DateOnly? CompletedDate { get; set; }
    public int? EstimatedDurationDays { get; set; }

    public decimal EstimatedCost { get; set; }

    // Impact — all optional, mostly meaningful for Investment work.
    public decimal? EstimatedValueIncrease { get; set; }
    public int? ExpectedLifespanYears { get; set; }
    public decimal? EnergyEfficiencyGainPercent { get; set; }

    public ContractorInfo? Contractor { get; set; }
    public List<ProjectCost> Costs { get; set; } = [];

    /// <summary>
    /// Nullable rather than defaulted to []: this was added after projects already existed, and a
    /// missing JSON property deserializes to the CLR default rather than running the initializer —
    /// the trap that crashed GetAll on Property.MemberUserIds. Always read it as `?? []`.
    /// Unlike Costs (written by every create/update since day one), this one can genuinely be absent.
    /// </summary>
    public List<ProjectMilestone>? Milestones { get; set; }

    public required string CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Derived, never stored: the actual cost *is* the sum of the itemised costs. Keeping a separate
    /// stored total would give two sources of truth that drift apart. If you don't want to itemise,
    /// a single cost row of type Other is the whole cost.
    /// </summary>
    public decimal ActualCost => Costs.Sum(c => c.Amount);
}

/// <summary>
/// Owned by Project — nested JSON, no container of its own. Per project by design: the same firm on
/// two jobs is entered twice. A reusable contractor register would be its own container, worth doing
/// only if that duplication starts to hurt.
/// </summary>
public class ContractorInfo
{
    public required string Name { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public decimal? QuotedPrice { get; set; }
    public DateOnly? QuotedDate { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Owned by Project — nested JSON, no container of its own. Purely a schedule: money stays in
/// ProjectCost. The original sketch carried estimated/actual cost per stage too, which would have
/// recreated exactly the two-sources-of-truth problem that makes Project.ActualCost computed.
/// </summary>
public class ProjectMilestone
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string Description { get; set; }
    public DateOnly? PlannedDate { get; set; }
    public DateOnly? CompletedDate { get; set; }
}

/// <summary>Owned by Project — nested JSON, no container of its own.</summary>
public class ProjectCost
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public CostType Type { get; set; }
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public DateOnly DateIncurred { get; set; }
    public bool IsBudgeted { get; set; }
}
