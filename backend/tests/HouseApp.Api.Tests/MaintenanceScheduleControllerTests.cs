using System.Net;
using System.Net.Http.Json;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Auth;
using HouseApp.Api.Dtos.Maintenance;
using HouseApp.Api.Dtos.Projects;
using HouseApp.Api.Dtos.PropertyComponents;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HouseApp.Api.Tests;

/// <summary>
/// The schedule is fully derived, so these are really tests of the derivation rules — there is no
/// stored state to round-trip. `asOf` exists so "overdue" and "due soon" can be asserted against a
/// fixed date rather than whenever the suite happens to run.
/// </summary>
public class MaintenanceScheduleControllerTests : IClassFixture<HouseAppWebApplicationFactory>
{
    private readonly HouseAppWebApplicationFactory _factory;
    private static readonly PasswordHasher<ApplicationUser> Hasher = new();

    public MaintenanceScheduleControllerTests(HouseAppWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var email = $"{Guid.NewGuid()}@example.com";
        const string password = "Secret123!";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = new ApplicationUser
            {
                Email = email,
                DisplayName = "Test User",
                PasswordHash = string.Empty,
                IsAdmin = true,
            };
            user.PasswordHash = Hasher.HashPassword(user, password);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return client;
    }

    private static async Task<string> CreateComponentAsync(HttpClient client, int? intervalMonths)
    {
        var response = await client.PostAsJsonAsync(
            "/api/property-components",
            new CreatePropertyComponentRequest($"Komponent {Guid.NewGuid()}", intervalMonths));
        return (await response.Content.ReadFromJsonAsync<PropertyComponentDto>())!.Id;
    }

    private static async Task<List<MaintenanceScheduleItemDto>> GetScheduleAsync(
        HttpClient client,
        string propertyId,
        DateOnly asOf)
    {
        var response = await client.GetAsync(
            $"/api/properties/{propertyId}/maintenance-schedule?asOf={asOf:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<MaintenanceScheduleItemDto>>())!;
    }

    [Fact]
    public async Task NextDue_IsLastCompletedMaintenancePlusTheComponentInterval()
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);
        var componentId = await CreateComponentAsync(client, intervalMonths: 12);

        await client.PostAsJsonAsync(
            $"/api/properties/{property.Id}/projects",
            TestData.SaveProject(
                "Servad panna",
                componentId,
                workType: WorkType.Maintenance,
                status: ProjectStatus.Completed,
                completedDate: new DateOnly(2025, 6, 1)));

        var item = (await GetScheduleAsync(client, property.Id, new DateOnly(2025, 7, 1)))
            .Single(i => i.ComponentId == componentId);

        Assert.Equal(new DateOnly(2025, 6, 1), item.LastCompletedDate);
        Assert.Equal(new DateOnly(2026, 6, 1), item.NextDueDate);
        Assert.Equal(MaintenanceUrgency.Ok, item.Urgency);
        Assert.Equal(11, item.MonthsUntilDue);
    }

    [Theory]
    // Due 2026-06-01; asserted from three vantage points.
    [InlineData("2026-07-01", MaintenanceUrgency.Overdue)]
    [InlineData("2026-04-15", MaintenanceUrgency.DueSoon)]
    [InlineData("2025-08-01", MaintenanceUrgency.Ok)]
    public async Task Urgency_ReflectsHowCloseTheDueDateIs(string asOf, MaintenanceUrgency expected)
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);
        var componentId = await CreateComponentAsync(client, intervalMonths: 12);

        await client.PostAsJsonAsync(
            $"/api/properties/{property.Id}/projects",
            TestData.SaveProject(
                "Servad panna",
                componentId,
                workType: WorkType.Maintenance,
                status: ProjectStatus.Completed,
                completedDate: new DateOnly(2025, 6, 1)));

        var item = (await GetScheduleAsync(client, property.Id, DateOnly.Parse(asOf)))
            .Single(i => i.ComponentId == componentId);

        Assert.Equal(expected, item.Urgency);
    }

    [Fact]
    public async Task ComponentWithNoInterval_IsNotScheduled()
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);
        var componentId = await CreateComponentAsync(client, intervalMonths: null);

        var item = (await GetScheduleAsync(client, property.Id, new DateOnly(2026, 1, 1)))
            .Single(i => i.ComponentId == componentId);

        Assert.Equal(MaintenanceUrgency.NotScheduled, item.Urgency);
        Assert.Null(item.NextDueDate);
    }

    [Fact]
    public async Task IntervalButNothingLogged_IsUnknownRatherThanOverdue()
    {
        // The work may well predate the app — claiming it's overdue would be a guess presented as a fact.
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);
        var componentId = await CreateComponentAsync(client, intervalMonths: 12);

        var item = (await GetScheduleAsync(client, property.Id, new DateOnly(2026, 1, 1)))
            .Single(i => i.ComponentId == componentId);

        Assert.Equal(MaintenanceUrgency.Unknown, item.Urgency);
        Assert.Null(item.LastCompletedDate);
        Assert.Null(item.NextDueDate);
    }

    [Fact]
    public async Task OnlyCompletedMaintenanceProjectsCount()
    {
        // A renovation of the same component isn't maintenance, and a planned job hasn't happened yet.
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);
        var componentId = await CreateComponentAsync(client, intervalMonths: 12);

        await client.PostAsJsonAsync(
            $"/api/properties/{property.Id}/projects",
            TestData.SaveProject("Renovering", componentId, workType: WorkType.Renovation,
                status: ProjectStatus.Completed, completedDate: new DateOnly(2025, 6, 1)));
        await client.PostAsJsonAsync(
            $"/api/properties/{property.Id}/projects",
            TestData.SaveProject("Planerad service", componentId, workType: WorkType.Maintenance,
                status: ProjectStatus.Planned));

        var item = (await GetScheduleAsync(client, property.Id, new DateOnly(2026, 1, 1)))
            .Single(i => i.ComponentId == componentId);

        Assert.Equal(MaintenanceUrgency.Unknown, item.Urgency);
        Assert.True(item.HasUpcomingProject);
    }

    [Fact]
    public async Task LastCompleted_UsesTheNewestOfSeveral()
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);
        var componentId = await CreateComponentAsync(client, intervalMonths: 12);

        foreach (var date in new[] { new DateOnly(2023, 1, 1), new DateOnly(2025, 6, 1), new DateOnly(2024, 3, 1) })
        {
            await client.PostAsJsonAsync(
                $"/api/properties/{property.Id}/projects",
                TestData.SaveProject($"Service {date:yyyy}", componentId, workType: WorkType.Maintenance,
                    status: ProjectStatus.Completed, completedDate: date));
        }

        var item = (await GetScheduleAsync(client, property.Id, new DateOnly(2025, 7, 1)))
            .Single(i => i.ComponentId == componentId);

        Assert.Equal(new DateOnly(2025, 6, 1), item.LastCompletedDate);
        Assert.Equal("Service 2025", item.LastProjectName);
    }

    [Fact]
    public async Task AnotherPropertysProjectsDoNotCount()
    {
        var client = await CreateAuthenticatedClientAsync();
        var a = await TestData.CreatePropertyAsync(client, "House A");
        var b = await TestData.CreatePropertyAsync(client, "House B");
        var componentId = await CreateComponentAsync(client, intervalMonths: 12);

        await client.PostAsJsonAsync(
            $"/api/properties/{a.Id}/projects",
            TestData.SaveProject("Servad hos A", componentId, workType: WorkType.Maintenance,
                status: ProjectStatus.Completed, completedDate: new DateOnly(2025, 6, 1)));

        var item = (await GetScheduleAsync(client, b.Id, new DateOnly(2026, 1, 1)))
            .Single(i => i.ComponentId == componentId);

        Assert.Null(item.LastCompletedDate);
    }

    [Fact]
    public async Task WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/properties/{Guid.NewGuid()}/maintenance-schedule");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
