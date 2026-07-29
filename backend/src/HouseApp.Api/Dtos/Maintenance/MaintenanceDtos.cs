using System.Text.Json.Serialization;

namespace HouseApp.Api.Dtos.Maintenance;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MaintenanceUrgency
{
    /// <summary>The component has no recommended interval, so nothing can be predicted.</summary>
    NotScheduled,

    /// <summary>Has an interval, but neither a completed maintenance project nor a build year to count from.</summary>
    Unknown,

    Ok,
    DueSoon,
    Overdue,
}

/// <summary>Where LastCompletedDate came from — the UI says so, because the two mean different things.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MaintenanceBaseline
{
    None,

    /// <summary>An actual completed maintenance project.</summary>
    Project,

    /// <summary>
    /// Nothing has been logged, so the property's build year stands in — the component is assumed to
    /// date from when the house was built. A starting point to correct, not a record of work done.
    /// </summary>
    YearBuilt,
}

/// <summary>
/// Entirely derived — there is no maintenanceSchedule container. Everything here comes from
/// PropertyComponent.RecommendedIntervalMonths plus the newest completed Maintenance project for
/// that component, so it can't drift out of step with the projects it describes.
/// </summary>
public record MaintenanceScheduleItemDto(
    string ComponentId,
    string ComponentName,
    int? RecommendedIntervalMonths,
    DateOnly? LastCompletedDate,
    MaintenanceBaseline Baseline,
    /// <summary>Id of the project LastCompletedDate came from — null when the baseline is the build year.</summary>
    string? LastProjectId,
    string? LastProjectName,
    DateOnly? NextDueDate,
    int? MonthsUntilDue,
    MaintenanceUrgency Urgency,
    /// <summary>True when a Planned/InProgress maintenance project already exists for the component.</summary>
    bool HasUpcomingProject);
