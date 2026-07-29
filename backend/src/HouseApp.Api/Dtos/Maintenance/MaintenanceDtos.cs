using System.Text.Json.Serialization;

namespace HouseApp.Api.Dtos.Maintenance;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MaintenanceUrgency
{
    /// <summary>The component has no recommended interval, so nothing can be predicted.</summary>
    NotScheduled,

    /// <summary>Has an interval but no completed maintenance project to count from.</summary>
    Unknown,

    Ok,
    DueSoon,
    Overdue,
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
    /// <summary>Id of the project LastCompletedDate came from, so the UI can link to it.</summary>
    string? LastProjectId,
    string? LastProjectName,
    DateOnly? NextDueDate,
    int? MonthsUntilDue,
    MaintenanceUrgency Urgency,
    /// <summary>True when a Planned/InProgress maintenance project already exists for the component.</summary>
    bool HasUpcomingProject);
