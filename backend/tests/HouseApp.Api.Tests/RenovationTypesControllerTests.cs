using System.Net;
using System.Net.Http.Json;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Auth;
using HouseApp.Api.Dtos.Properties;
using HouseApp.Api.Dtos.RenovationEntries;
using HouseApp.Api.Dtos.RenovationTypes;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HouseApp.Api.Tests;

public class RenovationTypesControllerTests : IClassFixture<HouseAppWebApplicationFactory>
{
    private readonly HouseAppWebApplicationFactory _factory;
    private static readonly PasswordHasher<ApplicationUser> Hasher = new();

    public RenovationTypesControllerTests(HouseAppWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Admin by default — creating/updating/deleting types requires it. Pass false to exercise the
    /// gate, and to check that reading stays open.
    /// </summary>
    private async Task<HttpClient> CreateAuthenticatedClientAsync(bool isAdmin = true)
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
                IsAdmin = isAdmin,
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

    [Fact]
    public async Task CreateThenList_RoundTripsRenovationType()
    {
        var client = await CreateAuthenticatedClientAsync();

        var create = await client.PostAsJsonAsync(
            "/api/renovation-types",
            new CreateRenovationTypeRequest("Fönsterbyte", 240));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<RenovationTypeDto>();
        Assert.NotNull(created);
        Assert.Equal(240, created!.RecommendedIntervalMonths);

        var list = await (await client.GetAsync("/api/renovation-types")).Content.ReadFromJsonAsync<List<RenovationTypeDto>>();
        Assert.Contains(list!, t => t.Id == created.Id && t.Name == "Fönsterbyte");
    }

    [Fact]
    public async Task Delete_WhenTypeIsInUse_ReturnsConflictAndDoesNotDelete()
    {
        var client = await CreateAuthenticatedClientAsync();

        var createType = await client.PostAsJsonAsync("/api/renovation-types", new CreateRenovationTypeRequest("Tak", 300));
        var type = await createType.Content.ReadFromJsonAsync<RenovationTypeDto>();

        var createProperty = await client.PostAsJsonAsync(
            "/api/properties",
            new CreatePropertyRequest("Test House", "1 Test St", new DateOnly(2020, 1, 1), 100000m));
        var property = await createProperty.Content.ReadFromJsonAsync<PropertyDto>();

        var createEntry = await client.PostAsJsonAsync(
            $"/api/properties/{property!.Id}/renovation-entries",
            new CreateRenovationEntryRequest(new DateOnly(2024, 1, 1), type!.Id, "Nytt tak", null, 50000m, null));
        Assert.Equal(HttpStatusCode.OK, createEntry.StatusCode);

        var delete = await client.DeleteAsync($"/api/renovation-types/{type.Id}");
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);

        var list = await (await client.GetAsync("/api/renovation-types")).Content.ReadFromJsonAsync<List<RenovationTypeDto>>();
        Assert.Contains(list!, t => t.Id == type.Id);
    }

    [Fact]
    public async Task Delete_WhenTypeIsUnused_Succeeds()
    {
        var client = await CreateAuthenticatedClientAsync();

        var createType = await client.PostAsJsonAsync("/api/renovation-types", new CreateRenovationTypeRequest("Oanvänd typ", null));
        var type = await createType.Content.ReadFromJsonAsync<RenovationTypeDto>();

        var delete = await client.DeleteAsync($"/api/renovation-types/{type!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    /// <summary>
    /// GetAll is deliberately NOT admin-gated — it feeds the type dropdown on the renovations page
    /// and in the dashboard quick-add modal, so gating it would stop regular users from creating
    /// entries at all. This is the regression that would be silent from the backend's point of view.
    /// </summary>
    [Fact]
    public async Task GetAll_IsAllowedForRegularUsers()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var createType = await admin.PostAsJsonAsync(
            "/api/renovation-types",
            new CreateRenovationTypeRequest($"Läsbar typ {Guid.NewGuid()}", 12));
        var type = await createType.Content.ReadFromJsonAsync<RenovationTypeDto>();

        var regular = await CreateAuthenticatedClientAsync(isAdmin: false);
        var response = await regular.GetAsync("/api/renovation-types");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<RenovationTypeDto>>();
        Assert.Contains(list!, t => t.Id == type!.Id);
    }

    [Fact]
    public async Task Mutations_ForRegularUser_ReturnForbidden()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var createType = await admin.PostAsJsonAsync(
            "/api/renovation-types",
            new CreateRenovationTypeRequest($"Skyddad typ {Guid.NewGuid()}", null));
        var type = await createType.Content.ReadFromJsonAsync<RenovationTypeDto>();

        var regular = await CreateAuthenticatedClientAsync(isAdmin: false);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await regular.PostAsJsonAsync("/api/renovation-types", new CreateRenovationTypeRequest("Ny", null))).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await regular.PutAsJsonAsync($"/api/renovation-types/{type!.Id}", new UpdateRenovationTypeRequest("Ändrad", null))).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await regular.DeleteAsync($"/api/renovation-types/{type.Id}")).StatusCode);
    }
}
