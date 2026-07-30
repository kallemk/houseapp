using System.Net;
using System.Net.Http.Json;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Auth;
using HouseApp.Api.Dtos.Budgets;
using HouseApp.Api.Dtos.Documents;
using HouseApp.Api.Dtos.Properties;
using HouseApp.Api.Dtos.PropertyComponents;
using HouseApp.Api.Dtos.Valuations;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HouseApp.Api.Tests;

/// <summary>
/// The reason this change exists: every per-property controller used to take a propertyId on trust,
/// which was harmless only while every account was a member of every property. These assert that a
/// stranger gets 404 from each of them — and that the demo property is the one deliberate exception.
///
/// Deliberately covers writes as well as reads: the leak isn't only about seeing another household's
/// figures, it's about being able to change them.
/// </summary>
public class PropertyAccessTests : IClassFixture<HouseAppWebApplicationFactory>
{
    private readonly HouseAppWebApplicationFactory _factory;
    private static readonly PasswordHasher<ApplicationUser> Hasher = new();

    public PropertyAccessTests(HouseAppWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(bool isAdmin = false)
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

    /// <summary>Every per-property endpoint, so a newly added controller shows up as a gap here.</summary>
    private static async Task<List<(string What, HttpResponseMessage Response)>> HitEveryEndpointAsync(
        HttpClient client,
        string propertyId)
    {
        var results = new List<(string, HttpResponseMessage)>
        {
            ("GET projects", await client.GetAsync($"/api/properties/{propertyId}/projects")),
            ("POST projects", await client.PostAsJsonAsync($"/api/properties/{propertyId}/projects",
                TestData.SaveProject("Smyginlagt projekt", "Roof"))),
            ("GET valuations", await client.GetAsync($"/api/properties/{propertyId}/valuations")),
            ("POST valuations", await client.PostAsJsonAsync($"/api/properties/{propertyId}/valuations",
                new CreateValuationEntryRequest(new DateOnly(2026, 1, 1), 1m, null, null))),
            ("GET documents", await client.GetAsync($"/api/properties/{propertyId}/documents")),
            // Property id travels in the body here, not the route — without a check this writes a
            // SAS URL into someone else's blob path.
            ("POST document upload-url", await client.PostAsJsonAsync("/api/documents/upload-url",
                new UploadUrlRequest(propertyId, "smyg.pdf", "application/pdf"))),
            ("POST documents", await client.PostAsJsonAsync("/api/documents",
                new CreateDocumentRequest(propertyId, null, new DateOnly(2026, 1, 1), "Smyginlagt", "smyg.pdf",
                    "application/pdf", $"{propertyId}/smyg.pdf", 1, DocumentCategory.Other))),
            ("GET budgets", await client.GetAsync($"/api/properties/{propertyId}/budgets")),
            ("GET budget year", await client.GetAsync($"/api/properties/{propertyId}/budgets/2026")),
            ("PUT budget", await client.PutAsJsonAsync($"/api/properties/{propertyId}/budgets/2026",
                new SaveBudgetRequest(2026, 1m, 1m, 1m))),
            ("GET maintenance-schedule", await client.GetAsync($"/api/properties/{propertyId}/maintenance-schedule")),
            ("GET components", await client.GetAsync($"/api/properties/{propertyId}/components")),
            ("POST components", await client.PostAsJsonAsync($"/api/properties/{propertyId}/components",
                new SavePropertyLocalComponentRequest("Smyginlagd komponent", 12))),
            ("POST components sync", await client.PostAsync($"/api/properties/{propertyId}/components/sync", null)),
            ("GET property", await client.GetAsync($"/api/properties/{propertyId}")),
        };

        return results;
    }

    [Fact]
    public async Task AnotherUsersProperty_Is404FromEveryPerPropertyEndpoint()
    {
        var owner = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(owner, "Privat hus");

        var stranger = await CreateAuthenticatedClientAsync();
        var results = await HitEveryEndpointAsync(stranger, property.Id);

        Assert.All(results, r => Assert.Equal(HttpStatusCode.NotFound, r.Response.StatusCode));
    }

    [Fact]
    public async Task TheDemoProperty_IsReachableByEveryone()
    {
        // The one deliberate exception, and a sandbox rather than a read-only tour — so the writes
        // in the list above must succeed too.
        var owner = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(owner, "Demo");

        var admin = await CreateAuthenticatedClientAsync(isAdmin: true);
        var flag = await admin.PutAsJsonAsync($"/api/properties/{property.Id}/demo", new SetDemoPropertyRequest(true));
        Assert.Equal(HttpStatusCode.NoContent, flag.StatusCode);

        var stranger = await CreateAuthenticatedClientAsync();
        var results = await HitEveryEndpointAsync(stranger, property.Id);

        Assert.All(results, r => Assert.True(
            r.Response.IsSuccessStatusCode,
            $"{r.What} returned {(int)r.Response.StatusCode} for the demo property"));
    }

    [Fact]
    public async Task DemoAccess_DoesNotAllowManagingMembersOrDeleting()
    {
        // Playing in the sandbox is not the same as owning it.
        var owner = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(owner, "Demo");
        var admin = await CreateAuthenticatedClientAsync(isAdmin: true);
        await admin.PutAsJsonAsync($"/api/properties/{property.Id}/demo", new SetDemoPropertyRequest(true));

        var stranger = await CreateAuthenticatedClientAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/properties/{property.Id}/members")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await stranger.GetAsync($"/api/properties/{property.Id}/member-candidates?query=te")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await stranger.PostAsJsonAsync($"/api/properties/{property.Id}/members", new AddPropertyMemberRequest("whoever"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"/api/properties/{property.Id}")).StatusCode);
    }

    [Fact]
    public async Task TheDemoProperty_CannotBeDeletedEvenByItsOwner()
    {
        var owner = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(owner, "Demo");
        var admin = await CreateAuthenticatedClientAsync(isAdmin: true);
        await admin.PutAsJsonAsync($"/api/properties/{property.Id}/demo", new SetDemoPropertyRequest(true));

        var delete = await owner.DeleteAsync($"/api/properties/{property.Id}");

        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
    }

    [Fact]
    public async Task SetDemo_IsAdminOnly_AndOnlyOnePropertyCanBeTheDemo()
    {
        var owner = await CreateAuthenticatedClientAsync();
        var first = await TestData.CreatePropertyAsync(owner, "Demo ett");
        var second = await TestData.CreatePropertyAsync(owner, "Demo två");

        // A member of the property still can't publish it to everyone.
        Assert.Equal(HttpStatusCode.Forbidden,
            (await owner.PutAsJsonAsync($"/api/properties/{first.Id}/demo", new SetDemoPropertyRequest(true))).StatusCode);

        var admin = await CreateAuthenticatedClientAsync(isAdmin: true);
        await admin.PutAsJsonAsync($"/api/properties/{first.Id}/demo", new SetDemoPropertyRequest(true));
        await admin.PutAsJsonAsync($"/api/properties/{second.Id}/demo", new SetDemoPropertyRequest(true));

        var list = await (await owner.GetAsync("/api/properties")).Content.ReadFromJsonAsync<List<PropertyDto>>();
        Assert.False(list!.Single(p => p.Id == first.Id).IsDemo);
        Assert.True(list.Single(p => p.Id == second.Id).IsDemo);
    }
}
