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

public class UsersControllerTests : IClassFixture<HouseAppWebApplicationFactory>
{
    private readonly HouseAppWebApplicationFactory _factory;
    private static readonly PasswordHasher<ApplicationUser> Hasher = new();

    public UsersControllerTests(HouseAppWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Admin by default, because every endpoint on this controller requires it — a regular client
    /// would just assert 403 everywhere. Pass false for the tests that check the gate itself.
    /// </summary>
    private async Task<(HttpClient Client, string UserId)> CreateAuthenticatedClientAsync(bool isAdmin = true)
    {
        var email = $"{Guid.NewGuid()}@example.com";
        const string password = "Secret123!";
        string userId;

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = new ApplicationUser { Email = email, DisplayName = "Test User", IsAdmin = isAdmin };
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

    [Fact]
    public async Task Create_AddsUserThatCanThenSignInWithGoogle()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();
        var newEmail = $"invited-{Guid.NewGuid()}@example.com";

        var create = await client.PostAsJsonAsync("/api/users", new CreateUserRequest(newEmail, "Invited Person", null));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var created = await create.Content.ReadFromJsonAsync<UserDto>();
        Assert.False(created!.HasPassword);

        // Being on the list is precisely what makes Google sign-in work.
        var googleClient = _factory.CreateClient();
        var googleLogin = await googleClient.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest(newEmail));
        Assert.Equal(HttpStatusCode.OK, googleLogin.StatusCode);
        Assert.Equal(created.Id, (await googleLogin.Content.ReadFromJsonAsync<MeResponse>())!.Id);
    }

    [Fact]
    public async Task Create_WithInitialPassword_AllowsPasswordLogin()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();
        var newEmail = $"withpw-{Guid.NewGuid()}@example.com";

        var create = await client.PostAsJsonAsync("/api/users", new CreateUserRequest(newEmail, "Has Password", "InitPw123!"));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        Assert.True((await create.Content.ReadFromJsonAsync<UserDto>())!.HasPassword);

        var loginClient = _factory.CreateClient();
        var login = await loginClient.PostAsJsonAsync("/api/auth/login", new LoginRequest(newEmail, "InitPw123!"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    /// <summary>
    /// Creating an account grants access to nothing. This used to do the opposite — every new user
    /// was backfilled onto every existing property — which was the sharing model when there were two
    /// accounts and one house, and became a data leak as soon as a second household joined.
    /// </summary>
    [Fact]
    public async Task Create_DoesNotGrantAccessToExistingProperties()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();

        var createProperty = await client.PostAsJsonAsync(
            "/api/properties",
            TestData.SaveProperty("Pre-existing House", "1 Old St"));
        var property = await createProperty.Content.ReadFromJsonAsync<PropertyDto>();

        var newEmail = $"newcomer-{Guid.NewGuid()}@example.com";
        await client.PostAsJsonAsync("/api/users", new CreateUserRequest(newEmail, "Late Joiner", null));

        var newClient = _factory.CreateClient();
        await newClient.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest(newEmail));

        var list = await (await newClient.GetAsync("/api/properties")).Content.ReadFromJsonAsync<List<PropertyDto>>();
        Assert.DoesNotContain(list!, p => p.Id == property!.Id);

        // Not just hidden from the listing — the property's data is unreachable too.
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await newClient.GetAsync($"/api/properties/{property!.Id}/projects")).StatusCode);
    }

    [Fact]
    public async Task Create_WithDuplicateEmail_ReturnsConflict()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();
        var email = $"dupe-{Guid.NewGuid()}@example.com";

        await client.PostAsJsonAsync("/api/users", new CreateUserRequest(email, "First", null));
        var second = await client.PostAsJsonAsync("/api/users", new CreateUserRequest(email.ToUpperInvariant(), "Second", null));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Delete_OwnAccount_IsBlocked()
    {
        var (client, userId) = await CreateAuthenticatedClientAsync();

        var response = await client.DeleteAsync($"/api/users/{userId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_OtherAccount_RemovesThemFromTheAllowlist()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();
        var newEmail = $"removeme-{Guid.NewGuid()}@example.com";
        var create = await client.PostAsJsonAsync("/api/users", new CreateUserRequest(newEmail, "Temp Person", null));
        var created = await create.Content.ReadFromJsonAsync<UserDto>();

        var delete = await client.DeleteAsync($"/api/users/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        // Removal from the users container must actually revoke Google sign-in.
        var googleClient = _factory.CreateClient();
        var googleLogin = await googleClient.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest(newEmail));
        Assert.Equal(HttpStatusCode.Forbidden, googleLogin.StatusCode);
    }

    [Fact]
    public async Task GetAll_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// The whole controller is admin-only, listing included — write access here is the power to
    /// grant and revoke access to the app, so a regular user shouldn't even see who else exists.
    /// </summary>
    [Fact]
    public async Task EveryEndpoint_ForRegularUser_ReturnsForbidden()
    {
        var (client, userId) = await CreateAuthenticatedClientAsync(isAdmin: false);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/users", new CreateUserRequest("x@example.com", "X", null))).StatusCode);
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await client.PutAsJsonAsync($"/api/users/{userId}", new UpdateUserRequest("X", true))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/api/users/{userId}")).StatusCode);
    }

    /// <summary>
    /// Authorization reads IsAdmin from the database rather than a claim baked into the cookie, so a
    /// promotion has to take effect on the promoted user's *existing* session — no re-login.
    /// </summary>
    [Fact]
    public async Task Update_PromotingUser_GrantsAccessWithoutSigningInAgain()
    {
        var (regularClient, regularUserId) = await CreateAuthenticatedClientAsync(isAdmin: false);
        Assert.Equal(HttpStatusCode.Forbidden, (await regularClient.GetAsync("/api/users")).StatusCode);

        var (adminClient, _) = await CreateAuthenticatedClientAsync();
        var promote = await adminClient.PutAsJsonAsync(
            $"/api/users/{regularUserId}",
            new UpdateUserRequest("Test User", true));
        Assert.Equal(HttpStatusCode.NoContent, promote.StatusCode);

        // Same client, same cookie, no second login.
        Assert.Equal(HttpStatusCode.OK, (await regularClient.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task Update_RemovingOwnAdminRights_IsBlocked()
    {
        // This is what makes "at least one admin always exists" hold: you can only ever demote
        // someone else, and that requires still being an admin yourself.
        var (client, userId) = await CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync($"/api/users/{userId}", new UpdateUserRequest("Test User", false));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var stillAdmin = await (await client.GetAsync("/api/users")).Content.ReadFromJsonAsync<List<UserDto>>();
        Assert.True(stillAdmin!.Single(u => u.Id == userId).IsAdmin);
    }

    [Fact]
    public async Task Update_DemotingSomeoneElse_RevokesTheirAccess()
    {
        var (otherClient, otherUserId) = await CreateAuthenticatedClientAsync();
        var (adminClient, _) = await CreateAuthenticatedClientAsync();

        var demote = await adminClient.PutAsJsonAsync(
            $"/api/users/{otherUserId}",
            new UpdateUserRequest("Test User", false));
        Assert.Equal(HttpStatusCode.NoContent, demote.StatusCode);

        // Takes effect immediately rather than lasting until their 14-day cookie expires.
        Assert.Equal(HttpStatusCode.Forbidden, (await otherClient.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task Create_MakesARegularUser_NotAnAdmin()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();

        var create = await client.PostAsJsonAsync(
            "/api/users",
            new CreateUserRequest($"regular-{Guid.NewGuid()}@example.com", "Regular Person", null));

        Assert.False((await create.Content.ReadFromJsonAsync<UserDto>())!.IsAdmin);
    }
}
