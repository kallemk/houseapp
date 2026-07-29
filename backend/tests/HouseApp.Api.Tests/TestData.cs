using System.Net.Http.Json;
using HouseApp.Api.Dtos.Projects;
using HouseApp.Api.Dtos.Properties;
using HouseApp.Api.Models;
using Xunit;

namespace HouseApp.Api.Tests;

/// <summary>
/// Builders for the two request shapes that got wide enough to drown the tests using them. Every
/// parameter has a default so a test only states the fields it actually cares about.
/// </summary>
public static class TestData
{
    public static SaveProjectRequest SaveProject(
        string name,
        string componentId,
        WorkType workType = WorkType.Renovation,
        ProjectStatus status = ProjectStatus.Planned,
        ProjectPriority priority = ProjectPriority.Medium,
        DateOnly? completedDate = null,
        decimal estimatedCost = 0m,
        ContractorInfoDto? contractor = null,
        List<ProjectCostRequest>? costs = null) =>
        new(
            Name: name,
            Description: null,
            Notes: null,
            WorkType: workType,
            ComponentId: componentId,
            Status: status,
            Priority: priority,
            IsUrgent: false,
            PlannedStartDate: null,
            ActualStartDate: null,
            CompletedDate: completedDate,
            EstimatedDurationDays: null,
            EstimatedCost: estimatedCost,
            EstimatedValueIncrease: null,
            ExpectedLifespanYears: null,
            EnergyEfficiencyGainPercent: null,
            Contractor: contractor,
            Costs: costs);

    public static CreatePropertyRequest CreatePropertyRequest(
        string nickname = "Test House",
        string address = "1 Test St") =>
        new(nickname, address, null, null, null, new DateOnly(2020, 1, 1), 100000m);

    public static async Task<PropertyDto> CreatePropertyAsync(
        HttpClient client,
        string nickname = "Test House",
        string address = "1 Test St")
    {
        var response = await client.PostAsJsonAsync("/api/properties", CreatePropertyRequest(nickname, address));
        var property = await response.Content.ReadFromJsonAsync<PropertyDto>();
        Assert.NotNull(property);
        return property!;
    }
}
