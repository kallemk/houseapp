using System.Net;
using System.Net.Http.Json;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Auth;
using HouseApp.Api.Dtos.Properties;
using HouseApp.Api.Dtos.Users;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HouseApp.Api.Tests;

public class PropertyMembersTests : IClassFixture<HouseAppWebApplicationFactory>
{
    private readonly HouseAppWebApplicationFactory _factory;
    private static readonly PasswordHasher<ApplicationUser> Hasher = new();

    public PropertyMembersTests(HouseAppWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<(HttpClient Client, string UserId)> CreateAuthenticatedClientAsync(
        string displayName = "Test User",
        string? email = null)
    {
        email ??= $"{Guid.NewGuid()}@example.com";
        const string password = "Secret123!";
        string userId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = new ApplicationUser
            {
                Email = email,
                DisplayName = displayName,
                PasswordHash = string.Empty,
            };
            user.PasswordHash = Hasher.HashPassword(user, password);
            db.Users.Add(user);
            await db.SaveChangesAsync();
            userId = user.Id;
        }

        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (client, userId);
    }

    private static async Task<List<PropertyMemberDto>> SearchAsync(HttpClient client, string propertyId, string query)
    {
        var response = await client.GetAsync(
            $"/api/properties/{propertyId}/member-candidates?query={Uri.EscapeDataString(query)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<PropertyMemberDto>>())!;
    }

    [Fact]
    public async Task NewProperty_HasOnlyItsCreatorAsMember()
    {
        var (owner, ownerId) = await CreateAuthenticatedClientAsync();
        await CreateAuthenticatedClientAsync(); // someone else exists and must not be added

        var property = await TestData.CreatePropertyAsync(owner);

        var members = await (await owner.GetAsync($"/api/properties/{property.Id}/members")).Content
            .ReadFromJsonAsync<List<PropertyMemberDto>>();

        var only = Assert.Single(members!);
        Assert.Equal(ownerId, only.UserId);
    }

    [Fact]
    public async Task AddingSomeone_GivesThemTheProjectDataToo()
    {
        // Membership is only meaningful if it unlocks the child endpoints, not just the listing.
        var (owner, _) = await CreateAuthenticatedClientAsync();
        var (guest, guestId) = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(owner);

        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/properties/{property.Id}/projects")).StatusCode);

        var add = await owner.PostAsJsonAsync($"/api/properties/{property.Id}/members", new AddPropertyMemberRequest(guestId));
        Assert.Equal(HttpStatusCode.NoContent, add.StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync($"/api/properties/{property.Id}/projects")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync($"/api/properties/{property.Id}")).StatusCode);
    }

    [Fact]
    public async Task AddingSomeoneTwice_ReturnsConflict()
    {
        var (owner, _) = await CreateAuthenticatedClientAsync();
        var (_, guestId) = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(owner);

        await owner.PostAsJsonAsync($"/api/properties/{property.Id}/members", new AddPropertyMemberRequest(guestId));
        var second = await owner.PostAsJsonAsync($"/api/properties/{property.Id}/members", new AddPropertyMemberRequest(guestId));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task RemovingAMember_RevokesTheirAccess()
    {
        var (owner, _) = await CreateAuthenticatedClientAsync();
        var (guest, guestId) = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(owner);
        await owner.PostAsJsonAsync($"/api/properties/{property.Id}/members", new AddPropertyMemberRequest(guestId));

        var remove = await owner.DeleteAsync($"/api/properties/{property.Id}/members/{guestId}");
        Assert.Equal(HttpStatusCode.NoContent, remove.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/properties/{property.Id}/projects")).StatusCode);
    }

    [Fact]
    public async Task RemovingTheLastMember_IsRefused()
    {
        // Admins deliberately don't bypass membership, so an empty member list would orphan the
        // property and everything in it permanently.
        var (owner, ownerId) = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(owner);

        var remove = await owner.DeleteAsync($"/api/properties/{property.Id}/members/{ownerId}");

        Assert.Equal(HttpStatusCode.Conflict, remove.StatusCode);
    }

    [Fact]
    public async Task NonMember_Gets404FromAllMemberEndpoints()
    {
        var (owner, ownerId) = await CreateAuthenticatedClientAsync();
        var (stranger, _) = await CreateAuthenticatedClientAsync();
        var property = await TestData.CreatePropertyAsync(owner);

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/properties/{property.Id}/members")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await stranger.GetAsync($"/api/properties/{property.Id}/member-candidates?query=test")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await stranger.PostAsJsonAsync($"/api/properties/{property.Id}/members", new AddPropertyMemberRequest(ownerId))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await stranger.DeleteAsync($"/api/properties/{property.Id}/members/{ownerId}")).StatusCode);
    }

    [Fact]
    public async Task Search_MatchesPartOfTheNameOrTheEmail_CaseInsensitively()
    {
        // The point of the search: you shouldn't need to know someone's exact address to invite them.
        var (owner, _) = await CreateAuthenticatedClientAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        await CreateAuthenticatedClientAsync($"Kalle Olsson {unique}", $"kalleolsson{unique}@gmail.com");
        var property = await TestData.CreatePropertyAsync(owner);

        Assert.Contains(await SearchAsync(owner, property.Id, "kalle"), m => m.Email.Contains(unique));
        Assert.Contains(await SearchAsync(owner, property.Id, "KALLE"), m => m.Email.Contains(unique));
        Assert.Contains(await SearchAsync(owner, property.Id, unique), m => m.Email.Contains(unique));
    }

    [Fact]
    public async Task Search_ReturnsNothingBelowTwoCharacters()
    {
        // The guard that keeps this a lookup rather than a way to dump the whole user directory.
        var (owner, _) = await CreateAuthenticatedClientAsync();
        await CreateAuthenticatedClientAsync("Someone Findable");
        var property = await TestData.CreatePropertyAsync(owner);

        Assert.Empty(await SearchAsync(owner, property.Id, ""));
        Assert.Empty(await SearchAsync(owner, property.Id, "s"));
        Assert.NotEmpty(await SearchAsync(owner, property.Id, "so"));
    }

    [Fact]
    public async Task Search_ExcludesPeopleAlreadyOnTheProperty()
    {
        var (owner, _) = await CreateAuthenticatedClientAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var (_, guestId) = await CreateAuthenticatedClientAsync($"Redan Med {unique}");
        var property = await TestData.CreatePropertyAsync(owner);

        Assert.Contains(await SearchAsync(owner, property.Id, unique), m => m.UserId == guestId);

        await owner.PostAsJsonAsync($"/api/properties/{property.Id}/members", new AddPropertyMemberRequest(guestId));

        Assert.DoesNotContain(await SearchAsync(owner, property.Id, unique), m => m.UserId == guestId);
    }

    [Fact]
    public async Task NewAccount_SeesNoPropertiesAtAll()
    {
        // Replaces the old backfill behaviour, which added every new account to every property.
        var (owner, _) = await CreateAuthenticatedClientAsync();
        await TestData.CreatePropertyAsync(owner, "Någon annans hus");

        var admin = await CreateAdminAsync();
        var newEmail = $"newcomer-{Guid.NewGuid()}@example.com";
        await admin.PostAsJsonAsync("/api/users", new CreateUserRequest(newEmail, "Newcomer", "Secret123!"));

        var newcomer = _factory.CreateClient();
        await newcomer.PostAsJsonAsync("/api/auth/login", new LoginRequest(newEmail, "Secret123!"));

        var list = await (await newcomer.GetAsync("/api/properties")).Content.ReadFromJsonAsync<List<PropertyDto>>();
        Assert.Empty(list!);
    }

    private async Task<HttpClient> CreateAdminAsync()
    {
        var email = $"admin-{Guid.NewGuid()}@example.com";
        const string password = "Secret123!";
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = new ApplicationUser
            {
                Email = email,
                DisplayName = "Admin",
                PasswordHash = string.Empty,
                IsAdmin = true,
            };
            user.PasswordHash = Hasher.HashPassword(user, password);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        return client;
    }
}
