using System.Net;
using System.Net.Http.Json;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Auth;
using HouseApp.Api.Dtos.Properties;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HouseApp.Api.Tests;

public class PropertiesControllerTests : IClassFixture<HouseAppWebApplicationFactory>
{
    private readonly HouseAppWebApplicationFactory _factory;
    private static readonly PasswordHasher<ApplicationUser> Hasher = new();

    public PropertiesControllerTests(HouseAppWebApplicationFactory factory)
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
            var user = new ApplicationUser { Email = email, DisplayName = "Test User", PasswordHash = string.Empty };
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
    public async Task CreateThenGet_RoundTripsProperty()
    {
        var client = await CreateAuthenticatedClientAsync();

        var create = await client.PostAsJsonAsync(
            "/api/properties",
            new CreatePropertyRequest("Our House", "123 Main St", new DateOnly(2020, 6, 1), 350000m));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var created = await create.Content.ReadFromJsonAsync<PropertyDto>();
        Assert.NotNull(created);

        var get = await client.GetAsync($"/api/properties/{created!.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var fetched = await get.Content.ReadFromJsonAsync<PropertyDto>();
        Assert.Equal("Our House", fetched!.Nickname);
    }

    [Fact]
    public async Task GetAll_WithoutAuth_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/properties");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_OnlyReturnsPropertiesCreatedWhileUserExisted()
    {
        // Property creation connects every account that exists at that moment. A user created
        // afterwards shouldn't retroactively see properties created before they existed.
        var clientA = await CreateAuthenticatedClientAsync();
        var createP1 = await clientA.PostAsJsonAsync(
            "/api/properties",
            new CreatePropertyRequest("House A", "1 Main St", new DateOnly(2020, 1, 1), 100000m));
        var p1 = await createP1.Content.ReadFromJsonAsync<PropertyDto>();

        var clientB = await CreateAuthenticatedClientAsync();
        var createP2 = await clientB.PostAsJsonAsync(
            "/api/properties",
            new CreatePropertyRequest("House B", "2 Main St", new DateOnly(2021, 1, 1), 200000m));
        var p2 = await createP2.Content.ReadFromJsonAsync<PropertyDto>();

        var bList = await (await clientB.GetAsync("/api/properties")).Content.ReadFromJsonAsync<List<PropertyDto>>();
        Assert.DoesNotContain(bList!, p => p.Id == p1!.Id);
        Assert.Contains(bList!, p => p.Id == p2!.Id);

        var aList = await (await clientA.GetAsync("/api/properties")).Content.ReadFromJsonAsync<List<PropertyDto>>();
        Assert.Contains(aList!, p => p.Id == p1!.Id);
        Assert.Contains(aList!, p => p.Id == p2!.Id);

        var bGetP1 = await clientB.GetAsync($"/api/properties/{p1!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, bGetP1.StatusCode);
    }
}
