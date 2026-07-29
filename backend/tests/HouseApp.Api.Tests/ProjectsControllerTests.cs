using System.Net;
using System.Net.Http.Json;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Auth;
using HouseApp.Api.Dtos.Projects;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HouseApp.Api.Tests;

public class ProjectsControllerTests : IClassFixture<HouseAppWebApplicationFactory>
{
    private readonly HouseAppWebApplicationFactory _factory;
    private static readonly PasswordHasher<ApplicationUser> Hasher = new();

    public ProjectsControllerTests(HouseAppWebApplicationFactory factory)
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
            // Admin because these tests create components to attach projects to.
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

    /// <summary>
    /// Costs and contractor are EF owned types stored as nested JSON — the one genuinely novel bit
    /// of mapping here, and the thing most likely to silently come back empty.
    /// </summary>
    [Fact]
    public async Task Create_RoundTripsNestedCostsAndContractor()
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);

        var request = TestData.SaveProject(
            "Takbyte",
            "Roof",
            workType: WorkType.Renovation,
            status: ProjectStatus.Completed,
            completedDate: new DateOnly(2025, 11, 14),
            estimatedCost: 180000m,
            contractor: new ContractorInfoDto("Tak AB", "070-1234567", "info@tak.se", null, 175000m, new DateOnly(2025, 9, 1), "Rekommenderad"),
            costs:
            [
                new ProjectCostRequest(CostType.Materials, "Takpannor", 120000m, new DateOnly(2025, 10, 1), true),
                new ProjectCostRequest(CostType.Labor, "Montering", 62000m, new DateOnly(2025, 11, 14), true),
            ]);

        var create = await client.PostAsJsonAsync($"/api/properties/{property.Id}/projects", request);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var fetched = await (await client.GetAsync(
            $"/api/projects/{(await create.Content.ReadFromJsonAsync<ProjectDto>())!.Id}?propertyId={property.Id}"))
            .Content.ReadFromJsonAsync<ProjectDto>();

        Assert.Equal(2, fetched!.Costs.Count);
        Assert.Contains(fetched.Costs, c => c.Type == CostType.Materials && c.Amount == 120000m);
        Assert.Equal("Tak AB", fetched.Contractor!.Name);
        Assert.Equal(175000m, fetched.Contractor.QuotedPrice);
        // Derived, not stored — the sum of the cost rows, not the estimate.
        Assert.Equal(182000m, fetched.ActualCost);
        Assert.Equal(180000m, fetched.EstimatedCost);
    }

    [Fact]
    public async Task Update_ReplacesCostsAndCanClearTheContractor()
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);

        var create = await client.PostAsJsonAsync(
            $"/api/properties/{property.Id}/projects",
            TestData.SaveProject(
                "Fasad",
                "Facade",
                contractor: new ContractorInfoDto("Måleri AB", null, null, null, null, null, null),
                costs: [new ProjectCostRequest(CostType.Labor, "Strykning", 40000m, new DateOnly(2025, 5, 1), false)]));
        var created = await create.Content.ReadFromJsonAsync<ProjectDto>();

        var update = await client.PutAsJsonAsync(
            $"/api/projects/{created!.Id}?propertyId={property.Id}",
            TestData.SaveProject(
                "Fasadbetsning",
                "Facade",
                status: ProjectStatus.InProgress,
                costs: [new ProjectCostRequest(CostType.Materials, "Färg", 12000m, new DateOnly(2025, 6, 1), false)]));
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var fetched = await (await client.GetAsync($"/api/projects/{created.Id}?propertyId={property.Id}"))
            .Content.ReadFromJsonAsync<ProjectDto>();

        Assert.Equal("Fasadbetsning", fetched!.Name);
        Assert.Equal(ProjectStatus.InProgress, fetched.Status);
        Assert.Null(fetched.Contractor);
        var cost = Assert.Single(fetched.Costs);
        Assert.Equal(CostType.Materials, cost.Type);
        Assert.Equal(12000m, fetched.ActualCost);
    }

    [Fact]
    public async Task Create_WithNoCosts_HasZeroActualCost()
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);

        var create = await client.PostAsJsonAsync(
            $"/api/properties/{property.Id}/projects",
            TestData.SaveProject("Planerat dräneringsjobb", "Drainage", estimatedCost: 90000m));

        var created = await create.Content.ReadFromJsonAsync<ProjectDto>();
        Assert.Empty(created!.Costs);
        Assert.Equal(0m, created.ActualCost);
        Assert.Null(created.Contractor);
    }

    [Fact]
    public async Task GetForProperty_ReturnsOnlyThatPropertysProjects()
    {
        var client = await CreateAuthenticatedClientAsync();
        var a = await TestData.CreatePropertyAsync(client, "House A");
        var b = await TestData.CreatePropertyAsync(client, "House B");

        await client.PostAsJsonAsync($"/api/properties/{a.Id}/projects", TestData.SaveProject("A-projekt", "Roof"));
        await client.PostAsJsonAsync($"/api/properties/{b.Id}/projects", TestData.SaveProject("B-projekt", "Roof"));

        var list = await (await client.GetAsync($"/api/properties/{a.Id}/projects")).Content
            .ReadFromJsonAsync<List<ProjectDto>>();

        Assert.Contains(list!, p => p.Name == "A-projekt");
        Assert.DoesNotContain(list!, p => p.Name == "B-projekt");
    }

    [Fact]
    public async Task Delete_RemovesTheProject()
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);

        var create = await client.PostAsJsonAsync(
            $"/api/properties/{property.Id}/projects",
            TestData.SaveProject("Tillfälligt", "Other"));
        var created = await create.Content.ReadFromJsonAsync<ProjectDto>();

        var delete = await client.DeleteAsync($"/api/projects/{created!.Id}?propertyId={property.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var get = await client.GetAsync($"/api/projects/{created.Id}?propertyId={property.Id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task GetForProperty_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/properties/{Guid.NewGuid()}/projects");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
