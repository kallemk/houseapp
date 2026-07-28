using System.Net;
using System.Net.Http.Json;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Auth;
using HouseApp.Api.Dtos.Documents;
using HouseApp.Api.Dtos.Properties;
using HouseApp.Api.Dtos.RenovationEntries;
using HouseApp.Api.Dtos.RenovationTypes;
using HouseApp.Api.Dtos.Valuations;
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
            // Admin because Delete_AlsoRemoves... builds its fixture via POST /api/renovation-types,
            // which is admin-only. Nothing on PropertiesController itself requires it.
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

    [Fact]
    public async Task Update_ChangesFields_AndIsVisibleOnGet()
    {
        var client = await CreateAuthenticatedClientAsync();

        var create = await client.PostAsJsonAsync(
            "/api/properties",
            new CreatePropertyRequest("Old Name", "1 Old St", new DateOnly(2020, 1, 1), 100000m));
        var created = await create.Content.ReadFromJsonAsync<PropertyDto>();

        var update = await client.PutAsJsonAsync(
            $"/api/properties/{created!.Id}",
            new UpdatePropertyRequest("New Name", "2 New St", new DateOnly(2021, 5, 5), 250000m));
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);

        var fetched = await (await client.GetAsync($"/api/properties/{created.Id}")).Content
            .ReadFromJsonAsync<PropertyDto>();
        Assert.Equal("New Name", fetched!.Nickname);
        Assert.Equal("2 New St", fetched.Address);
        Assert.Equal(new DateOnly(2021, 5, 5), fetched.PurchaseDate);
        Assert.Equal(250000m, fetched.PurchasePrice);
    }

    [Fact]
    public async Task UpdateAndDelete_OnSomeoneElsesProperty_ReturnNotFound()
    {
        var owner = await CreateAuthenticatedClientAsync();
        var create = await owner.PostAsJsonAsync(
            "/api/properties",
            new CreatePropertyRequest("Theirs", "1 Private Rd", new DateOnly(2020, 1, 1), 100000m));
        var property = await create.Content.ReadFromJsonAsync<PropertyDto>();

        // Created before this user existed, so they're not in MemberUserIds.
        var outsider = await CreateAuthenticatedClientAsync();

        var update = await outsider.PutAsJsonAsync(
            $"/api/properties/{property!.Id}",
            new UpdatePropertyRequest("Hijacked", "x", new DateOnly(2020, 1, 1), 1m));
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);

        var delete = await outsider.DeleteAsync($"/api/properties/{property.Id}");
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);

        // And it really is still there for its owner.
        var stillThere = await owner.GetAsync($"/api/properties/{property.Id}");
        Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);
    }

    [Fact]
    public async Task Delete_AlsoRemovesValuationsRenovationsAndDocuments()
    {
        // Cosmos has no cascade delete, so this has to be done by hand in the controller. Without
        // it the child documents survive in their own containers permanently and unreachably —
        // there's no longer a property to browse them through.
        var client = await CreateAuthenticatedClientAsync();

        var create = await client.PostAsJsonAsync(
            "/api/properties",
            new CreatePropertyRequest("Doomed", "1 Gone St", new DateOnly(2020, 1, 1), 100000m));
        var property = await create.Content.ReadFromJsonAsync<PropertyDto>();
        var propertyId = property!.Id;

        await client.PostAsJsonAsync(
            $"/api/properties/{propertyId}/valuations",
            new CreateValuationEntryRequest(new DateOnly(2022, 1, 1), 150000m, "Mäklare", null));

        var type = await (await client.PostAsJsonAsync("/api/renovation-types", new CreateRenovationTypeRequest($"Typ {Guid.NewGuid()}", null)))
            .Content.ReadFromJsonAsync<RenovationTypeDto>();
        await client.PostAsJsonAsync(
            $"/api/properties/{propertyId}/renovation-entries",
            new CreateRenovationEntryRequest(new DateOnly(2022, 2, 1), type!.Id, "Nytt tak", null, 50000m, null));

        await client.PostAsJsonAsync(
            "/api/documents",
            new CreateDocumentRequest(propertyId, null, new DateOnly(2022, 3, 1), "kvitto.pdf", "application/pdf", $"{propertyId}/kvitto.pdf", 1024, DocumentCategory.Receipt));

        var delete = await client.DeleteAsync($"/api/properties/{propertyId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(db.ValuationEntries.Where(v => v.PropertyId == propertyId).ToList());
        Assert.Empty(db.RenovationEntries.Where(r => r.PropertyId == propertyId).ToList());
        Assert.Empty(db.Documents.Where(d => d.PropertyId == propertyId).ToList());
        Assert.Null(await db.Properties.FindAsync(propertyId));
    }

    [Fact]
    public async Task GetAll_SkipsPropertyWithNullMemberUserIds_WithoutThrowing()
    {
        // Regression test: properties that existed before MemberUserIds was added deserialize
        // from Cosmos with a null (not empty) list, since a missing JSON property becomes the CLR
        // default rather than the "= []" field initializer. GetAll previously called
        // .Contains() directly on that null and threw a 500 for every request.
        var client = await CreateAuthenticatedClientAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Properties.Add(new Property
            {
                Nickname = "Legacy House",
                Address = "Old St",
                PurchaseDate = new DateOnly(2018, 1, 1),
                PurchasePrice = 100000m,
                MemberUserIds = null,
            });
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/properties");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<PropertyDto>>();
        Assert.DoesNotContain(list!, p => p.Nickname == "Legacy House");
    }
}
