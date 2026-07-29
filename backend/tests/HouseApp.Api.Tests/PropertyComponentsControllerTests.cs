using System.Net;
using System.Net.Http.Json;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Auth;
using HouseApp.Api.Dtos.PropertyComponents;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HouseApp.Api.Tests;

public class PropertyComponentsControllerTests : IClassFixture<HouseAppWebApplicationFactory>
{
    private readonly HouseAppWebApplicationFactory _factory;
    private static readonly PasswordHasher<ApplicationUser> Hasher = new();

    public PropertyComponentsControllerTests(HouseAppWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Admin by default — creating/updating/deleting components requires it. Pass false to exercise
    /// the gate, and to check that reading stays open.
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
    public async Task CreateThenList_RoundTripsComponent()
    {
        var client = await CreateAuthenticatedClientAsync();

        var create = await client.PostAsJsonAsync(
            "/api/property-components",
            new CreatePropertyComponentRequest("Altan", 240));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<PropertyComponentDto>();
        Assert.Equal(240, created!.RecommendedIntervalMonths);

        var list = await (await client.GetAsync("/api/property-components")).Content
            .ReadFromJsonAsync<List<PropertyComponentDto>>();
        Assert.Contains(list!, c => c.Id == created.Id && c.Name == "Altan");
    }

    [Fact]
    public async Task Delete_WhenComponentIsInUse_ReturnsConflictAndDoesNotDelete()
    {
        var client = await CreateAuthenticatedClientAsync();

        var createComponent = await client.PostAsJsonAsync(
            "/api/property-components",
            new CreatePropertyComponentRequest("Tak", 300));
        var component = await createComponent.Content.ReadFromJsonAsync<PropertyComponentDto>();

        var property = await TestData.CreatePropertyAsync(client, "Test House");
        var createProject = await client.PostAsJsonAsync(
            $"/api/properties/{property.Id}/projects",
            TestData.SaveProject("Nytt tak", component!.Id));
        Assert.Equal(HttpStatusCode.OK, createProject.StatusCode);

        var delete = await client.DeleteAsync($"/api/property-components/{component.Id}");
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);

        var list = await (await client.GetAsync("/api/property-components")).Content
            .ReadFromJsonAsync<List<PropertyComponentDto>>();
        Assert.Contains(list!, c => c.Id == component.Id);
    }

    [Fact]
    public async Task Delete_WhenComponentIsUnused_Succeeds()
    {
        var client = await CreateAuthenticatedClientAsync();

        var create = await client.PostAsJsonAsync(
            "/api/property-components",
            new CreatePropertyComponentRequest("Oanvänd komponent", null));
        var component = await create.Content.ReadFromJsonAsync<PropertyComponentDto>();

        var delete = await client.DeleteAsync($"/api/property-components/{component!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    /// <summary>
    /// GetAll is deliberately NOT admin-gated — it feeds the component dropdown when creating or
    /// editing a project, so gating it would stop regular users logging work at all. This is the
    /// regression that would look like a success from the backend's point of view.
    /// </summary>
    [Fact]
    public async Task GetAll_IsAllowedForRegularUsers()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var create = await admin.PostAsJsonAsync(
            "/api/property-components",
            new CreatePropertyComponentRequest($"Läsbar {Guid.NewGuid()}", 12));
        var component = await create.Content.ReadFromJsonAsync<PropertyComponentDto>();

        var regular = await CreateAuthenticatedClientAsync(isAdmin: false);
        var response = await regular.GetAsync("/api/property-components");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<PropertyComponentDto>>();
        Assert.Contains(list!, c => c.Id == component!.Id);
    }

    [Fact]
    public async Task Mutations_ForRegularUser_ReturnForbidden()
    {
        var admin = await CreateAuthenticatedClientAsync();
        var create = await admin.PostAsJsonAsync(
            "/api/property-components",
            new CreatePropertyComponentRequest($"Skyddad {Guid.NewGuid()}", null));
        var component = await create.Content.ReadFromJsonAsync<PropertyComponentDto>();

        var regular = await CreateAuthenticatedClientAsync(isAdmin: false);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await regular.PostAsJsonAsync("/api/property-components", new CreatePropertyComponentRequest("Ny", null))).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await regular.PutAsJsonAsync($"/api/property-components/{component!.Id}", new UpdatePropertyComponentRequest("Ändrad", null))).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await regular.DeleteAsync($"/api/property-components/{component.Id}")).StatusCode);
    }
}
