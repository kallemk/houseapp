using System.Net;
using System.Net.Http.Json;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Auth;
using HouseApp.Api.Dtos.Budgets;
using HouseApp.Api.Dtos.Projects;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HouseApp.Api.Tests;

public class BudgetsControllerTests : IClassFixture<HouseAppWebApplicationFactory>
{
    private readonly HouseAppWebApplicationFactory _factory;
    private static readonly PasswordHasher<ApplicationUser> Hasher = new();

    public BudgetsControllerTests(HouseAppWebApplicationFactory factory)
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

    private static decimal SpentOn(BudgetDto budget, WorkType workType) =>
        budget.Lines.Single(l => l.WorkType == workType).Spent;

    [Fact]
    public async Task Save_RoundTripsTheBudgetedAmounts()
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);

        var save = await client.PutAsJsonAsync(
            $"/api/properties/{property.Id}/budgets/2026",
            new SaveBudgetRequest(2026, 30000m, 150000m, 50000m));
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var budget = await (await client.GetAsync($"/api/properties/{property.Id}/budgets/2026"))
            .Content.ReadFromJsonAsync<BudgetDto>();

        Assert.Equal(230000m, budget!.TotalBudgeted);
        Assert.Equal(150000m, budget.Lines.Single(l => l.WorkType == WorkType.Renovation).Budgeted);
    }

    [Fact]
    public async Task Save_Twice_UpdatesRatherThanDuplicating()
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);

        await client.PutAsJsonAsync(
            $"/api/properties/{property.Id}/budgets/2026",
            new SaveBudgetRequest(2026, 1000m, 0m, 0m));
        await client.PutAsJsonAsync(
            $"/api/properties/{property.Id}/budgets/2026",
            new SaveBudgetRequest(2026, 2000m, 0m, 0m));

        var all = await (await client.GetAsync($"/api/properties/{property.Id}/budgets"))
            .Content.ReadFromJsonAsync<List<BudgetDto>>();

        var year2026 = Assert.Single(all!, b => b.Year == 2026);
        Assert.Equal(2000m, year2026.TotalBudgeted);
    }

    /// <summary>
    /// Spend is summed from cost rows on every read rather than stored, so editing a project has to
    /// be reflected immediately — a stored total would be stale from this point on.
    /// </summary>
    [Fact]
    public async Task Spent_IsSummedFromProjectCostsByWorkType()
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);

        await client.PostAsJsonAsync(
            $"/api/properties/{property.Id}/projects",
            TestData.SaveProject("Servad panna", "Heating", workType: WorkType.Maintenance,
                costs: [new ProjectCostRequest(CostType.Labor, null, 8000m, new DateOnly(2026, 3, 1), true)]));
        await client.PostAsJsonAsync(
            $"/api/properties/{property.Id}/projects",
            TestData.SaveProject("Nytt tak", "Roof", workType: WorkType.Renovation,
                costs:
                [
                    new ProjectCostRequest(CostType.Materials, null, 120000m, new DateOnly(2026, 4, 1), true),
                    new ProjectCostRequest(CostType.Labor, null, 60000m, new DateOnly(2026, 5, 1), true),
                ]));

        var budget = await (await client.GetAsync($"/api/properties/{property.Id}/budgets/2026"))
            .Content.ReadFromJsonAsync<BudgetDto>();

        Assert.Equal(8000m, SpentOn(budget!, WorkType.Maintenance));
        Assert.Equal(180000m, SpentOn(budget, WorkType.Renovation));
        Assert.Equal(0m, SpentOn(budget, WorkType.Investment));
        Assert.Equal(188000m, budget.TotalSpent);
    }

    /// <summary>
    /// A cost belongs to the year it was incurred, not the year the project finished — so a job
    /// spanning New Year splits the way the money actually did.
    /// </summary>
    [Fact]
    public async Task Spent_SplitsAProjectAcrossYearsByCostDate()
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);

        await client.PostAsJsonAsync(
            $"/api/properties/{property.Id}/projects",
            TestData.SaveProject("Vinterjobb", "Facade", workType: WorkType.Renovation,
                status: ProjectStatus.Completed, completedDate: new DateOnly(2027, 2, 1),
                costs:
                [
                    new ProjectCostRequest(CostType.Materials, null, 40000m, new DateOnly(2026, 12, 10), false),
                    new ProjectCostRequest(CostType.Labor, null, 25000m, new DateOnly(2027, 1, 20), false),
                ]));

        var y2026 = await (await client.GetAsync($"/api/properties/{property.Id}/budgets/2026"))
            .Content.ReadFromJsonAsync<BudgetDto>();
        var y2027 = await (await client.GetAsync($"/api/properties/{property.Id}/budgets/2027"))
            .Content.ReadFromJsonAsync<BudgetDto>();

        Assert.Equal(40000m, SpentOn(y2026!, WorkType.Renovation));
        Assert.Equal(25000m, SpentOn(y2027!, WorkType.Renovation));
    }

    [Fact]
    public async Task GetForYear_WithNoSavedBudget_StillReportsSpend()
    {
        // Spending without a plan should be visible, not hidden behind a missing budget row.
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);

        await client.PostAsJsonAsync(
            $"/api/properties/{property.Id}/projects",
            TestData.SaveProject("Oplanerat", "Other", workType: WorkType.Maintenance,
                costs: [new ProjectCostRequest(CostType.Other, null, 5000m, new DateOnly(2026, 8, 1), false)]));

        var budget = await (await client.GetAsync($"/api/properties/{property.Id}/budgets/2026"))
            .Content.ReadFromJsonAsync<BudgetDto>();

        Assert.Null(budget!.Id);
        Assert.Equal(0m, budget.TotalBudgeted);
        Assert.Equal(5000m, budget.TotalSpent);
        Assert.Equal(-5000m, budget.Lines.Single(l => l.WorkType == WorkType.Maintenance).Remaining);
    }

    [Fact]
    public async Task GetAll_IncludesYearsWithSpendButNoBudget()
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);

        await client.PostAsJsonAsync(
            $"/api/properties/{property.Id}/projects",
            TestData.SaveProject("Gammalt", "Other", workType: WorkType.Maintenance,
                costs: [new ProjectCostRequest(CostType.Other, null, 1000m, new DateOnly(2019, 5, 1), false)]));

        var all = await (await client.GetAsync($"/api/properties/{property.Id}/budgets"))
            .Content.ReadFromJsonAsync<List<BudgetDto>>();

        Assert.Contains(all!, b => b.Year == 2019);
    }

    [Fact]
    public async Task Save_WithMismatchedYear_ReturnsBadRequest()
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);

        var response = await client.PutAsJsonAsync(
            $"/api/properties/{property.Id}/budgets/2026",
            new SaveBudgetRequest(2027, 0m, 0m, 0m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnotherPropertysBudgetsAreSeparate()
    {
        var client = await CreateAuthenticatedClientAsync();
        var a = await TestData.CreatePropertyAsync(client, "House A");
        var b = await TestData.CreatePropertyAsync(client, "House B");

        await client.PutAsJsonAsync($"/api/properties/{a.Id}/budgets/2026", new SaveBudgetRequest(2026, 5000m, 0m, 0m));

        var budgetB = await (await client.GetAsync($"/api/properties/{b.Id}/budgets/2026"))
            .Content.ReadFromJsonAsync<BudgetDto>();

        Assert.Equal(0m, budgetB!.TotalBudgeted);
    }

    [Fact]
    public async Task GetForYear_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/properties/{Guid.NewGuid()}/budgets/2026");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
