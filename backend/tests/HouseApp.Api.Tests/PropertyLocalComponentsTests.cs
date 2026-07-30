using System.Net;
using System.Net.Http.Json;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Auth;
using HouseApp.Api.Dtos.Maintenance;
using HouseApp.Api.Dtos.PropertyComponents;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HouseApp.Api.Tests;

/// <summary>
/// A property either follows the central component registry or has its own list. Most of what can go
/// wrong here is about the moment it switches from one to the other, so that's what these cover.
/// </summary>
public class PropertyLocalComponentsTests : IClassFixture<HouseAppWebApplicationFactory>
{
    private readonly HouseAppWebApplicationFactory _factory;
    private static readonly PasswordHasher<ApplicationUser> Hasher = new();

    public PropertyLocalComponentsTests(HouseAppWebApplicationFactory factory)
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

    private static async Task<PropertyComponentDto> CreateCentralAsync(HttpClient client, int? intervalMonths = 12)
    {
        var response = await client.PostAsJsonAsync(
            "/api/property-components",
            new CreatePropertyComponentRequest($"Komponent {Guid.NewGuid()}", intervalMonths));
        return (await response.Content.ReadFromJsonAsync<PropertyComponentDto>())!;
    }

    private static async Task<PropertyComponentSetDto> GetSetAsync(HttpClient client, string propertyId)
    {
        var response = await client.GetAsync($"/api/properties/{propertyId}/components");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PropertyComponentSetDto>())!;
    }

    [Fact]
    public async Task ANewProperty_FollowsTheCentralRegistry()
    {
        var client = await CreateAuthenticatedClientAsync();
        var central = await CreateCentralAsync(client);
        var property = await TestData.CreatePropertyAsync(client);

        var set = await GetSetAsync(client, property.Id);

        Assert.False(set.Customized);
        Assert.Equal(0, set.AvailableFromCentralCount);
        var row = set.Components.Single(c => c.Id == central.Id);
        Assert.Equal(ComponentOrigin.Central, row.Origin);
        Assert.Equal(central.Name, row.Name);
    }

    [Fact]
    public async Task EditingOneComponent_CopiesTheWholeCentralSetDown()
    {
        // The trap this guards: materialising only the row being edited would silently drop every
        // other component the moment someone changed one interval.
        var client = await CreateAuthenticatedClientAsync();
        var edited = await CreateCentralAsync(client, intervalMonths: 12);
        var untouched = await CreateCentralAsync(client, intervalMonths: 24);
        var property = await TestData.CreatePropertyAsync(client);

        var update = await client.PutAsJsonAsync(
            $"/api/properties/{property.Id}/components/{edited.Id}",
            new SavePropertyLocalComponentRequest("Vårt tak", 60));
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var set = await GetSetAsync(client, property.Id);
        Assert.True(set.Customized);

        var editedRow = set.Components.Single(c => c.Id == edited.Id);
        Assert.Equal(ComponentOrigin.Modified, editedRow.Origin);
        Assert.Equal("Vårt tak", editedRow.Name);
        Assert.Equal(60, editedRow.RecommendedIntervalMonths);
        // Reported alongside, so the UI can say what it differs from.
        Assert.Equal(edited.Name, editedRow.CentralName);
        Assert.Equal(12, editedRow.CentralIntervalMonths);

        var untouchedRow = set.Components.Single(c => c.Id == untouched.Id);
        Assert.Equal(ComponentOrigin.Central, untouchedRow.Origin);
    }

    [Fact]
    public async Task AComponentAddedHere_IsLocalAndSurvivesASync()
    {
        var client = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(client);

        var created = await client.PostAsJsonAsync(
            $"/api/properties/{property.Id}/components",
            new SavePropertyLocalComponentRequest("Jordvärmepump", 120));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var component = (await created.Content.ReadFromJsonAsync<PropertyLocalComponentDto>())!;
        Assert.Equal(ComponentOrigin.Local, component.Origin);

        var sync = await client.PostAsync($"/api/properties/{property.Id}/components/sync", null);
        Assert.Equal(HttpStatusCode.OK, sync.StatusCode);

        var set = (await sync.Content.ReadFromJsonAsync<PropertyComponentSetDto>())!;
        var row = set.Components.Single(c => c.Id == component.Id);
        Assert.Equal(ComponentOrigin.Local, row.Origin);
        Assert.Equal("Jordvärmepump", row.Name);
    }

    [Fact]
    public async Task Sync_OverwritesLocalEditsAndAddsNewCentralComponents()
    {
        var client = await CreateAuthenticatedClientAsync();
        var shared = await CreateCentralAsync(client, intervalMonths: 12);
        var property = await TestData.CreatePropertyAsync(client);

        await client.PutAsJsonAsync(
            $"/api/properties/{property.Id}/components/{shared.Id}",
            new SavePropertyLocalComponentRequest("Lokalt namn", 999));

        // Added centrally *after* this property took its own copy — the case the sync button exists
        // for, since an uncustomised property would have picked it up on its own.
        var addedLater = await CreateCentralAsync(client, intervalMonths: 36);

        var before = await GetSetAsync(client, property.Id);
        Assert.Equal(1, before.AvailableFromCentralCount);
        Assert.DoesNotContain(before.Components, c => c.Id == addedLater.Id);

        var sync = await client.PostAsync($"/api/properties/{property.Id}/components/sync", null);
        var set = (await sync.Content.ReadFromJsonAsync<PropertyComponentSetDto>())!;

        var overwritten = set.Components.Single(c => c.Id == shared.Id);
        Assert.Equal(ComponentOrigin.Central, overwritten.Origin);
        Assert.Equal(shared.Name, overwritten.Name);
        Assert.Equal(12, overwritten.RecommendedIntervalMonths);

        Assert.Contains(set.Components, c => c.Id == addedLater.Id);
        Assert.Equal(0, set.AvailableFromCentralCount);
    }

    [Fact]
    public async Task RemovingEveryComponent_DoesNotRestoreTheCentralSet()
    {
        // The ProjectMigrator lesson, in miniature: "this property has no components" must not be
        // read as "this property has never been customised", or a deliberate clear-out would come
        // back on the next page load.
        var client = await CreateAuthenticatedClientAsync();
        await CreateCentralAsync(client);
        var property = await TestData.CreatePropertyAsync(client);

        foreach (var component in (await GetSetAsync(client, property.Id)).Components)
        {
            var delete = await client.DeleteAsync($"/api/properties/{property.Id}/components/{component.Id}");
            Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        }

        var set = await GetSetAsync(client, property.Id);
        Assert.True(set.Customized);
        Assert.Empty(set.Components);
    }

    [Fact]
    public async Task RemovingAComponentAProjectUsesIsRefused()
    {
        var client = await CreateAuthenticatedClientAsync();
        var central = await CreateCentralAsync(client);
        var property = await TestData.CreatePropertyAsync(client);

        // The project references the central id, which is exactly the id the local copy keeps.
        var project = await client.PostAsJsonAsync(
            $"/api/properties/{property.Id}/projects",
            TestData.SaveProject("Omläggning", central.Id));
        Assert.True(project.IsSuccessStatusCode);

        var delete = await client.DeleteAsync($"/api/properties/{property.Id}/components/{central.Id}");

        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
    }

    [Fact]
    public async Task AnotherPropertysProjects_DoNotBlockRemoval()
    {
        var client = await CreateAuthenticatedClientAsync();
        var central = await CreateCentralAsync(client);
        var withProject = await TestData.CreatePropertyAsync(client, "Med projekt");
        var other = await TestData.CreatePropertyAsync(client, "Utan projekt");

        await client.PostAsJsonAsync(
            $"/api/properties/{withProject.Id}/projects",
            TestData.SaveProject("Omläggning", central.Id));

        var delete = await client.DeleteAsync($"/api/properties/{other.Id}/components/{central.Id}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task TheMaintenanceSchedule_UsesThePropertysOwnInterval()
    {
        // The whole point of the feature: central says one thing, this house says another, and the
        // schedule follows the house.
        var client = await CreateAuthenticatedClientAsync();
        var central = await CreateCentralAsync(client, intervalMonths: 12);
        var property = await TestData.CreatePropertyAsync(client, yearBuilt: 2020);

        await client.PutAsJsonAsync(
            $"/api/properties/{property.Id}/components/{central.Id}",
            new SavePropertyLocalComponentRequest(central.Name, 240));

        var response = await client.GetAsync(
            $"/api/properties/{property.Id}/maintenance-schedule?asOf=2026-01-01");
        var schedule = (await response.Content.ReadFromJsonAsync<List<MaintenanceScheduleItemDto>>())!;

        var item = schedule.Single(i => i.ComponentId == central.Id);
        Assert.Equal(240, item.RecommendedIntervalMonths);
        // 2020-01-01 (build year) + 240 months, not the central 12 that would make it long overdue.
        Assert.Equal(new DateOnly(2040, 1, 1), item.NextDueDate);
        Assert.Equal(MaintenanceUrgency.Ok, item.Urgency);
    }

    [Fact]
    public async Task DeletingACentralComponent_LeavesACustomisedPropertysCopyAsLocal()
    {
        var client = await CreateAuthenticatedClientAsync();
        var central = await CreateCentralAsync(client);
        var property = await TestData.CreatePropertyAsync(client);

        await client.PutAsJsonAsync(
            $"/api/properties/{property.Id}/components/{central.Id}",
            new SavePropertyLocalComponentRequest("Behålls här", 12));

        var delete = await client.DeleteAsync($"/api/property-components/{central.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var set = await GetSetAsync(client, property.Id);
        var row = set.Components.Single(c => c.Id == central.Id);
        Assert.Equal(ComponentOrigin.Local, row.Origin);
        Assert.Equal("Behålls här", row.Name);
    }
}
