using System.Net;
using System.Net.Http.Json;
using HouseApp.Api.Dtos.Auth;
using Xunit;

namespace HouseApp.Api.Tests;

// The production limits are raised for every other suite; these bring one back down at a time. One
// factory per limit, because every test-server request shares one "address" and one fixed window:
// with both limits low, whichever test ran first would spend the other's budget.

public class LowSignInLimitFactory : HouseAppWebApplicationFactory
{
    protected override IReadOnlyDictionary<string, int> RateLimitSettings { get; } = new Dictionary<string, int>
    {
        ["RequestsPerMinute"] = 1_000_000,
        ["SignInsPerMinute"] = 3,
        ["ExportsPerHour"] = 1_000_000,
        ["UploadsPerHour"] = 1_000_000,
    };
}

public class LowGlobalLimitFactory : HouseAppWebApplicationFactory
{
    protected override IReadOnlyDictionary<string, int> RateLimitSettings { get; } = new Dictionary<string, int>
    {
        ["RequestsPerMinute"] = 5,
        ["SignInsPerMinute"] = 1_000_000,
        ["ExportsPerHour"] = 1_000_000,
        ["UploadsPerHour"] = 1_000_000,
    };
}

public class SignInRateLimitTests(LowSignInLimitFactory factory) : IClassFixture<LowSignInLimitFactory>
{
    [Fact]
    public async Task RepeatedSignIns_AreRefusedWith429()
    {
        // A wrong password still spends an attempt — that's the point of limiting it.
        var client = factory.CreateClient();
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("nobody@example.com", "wrong"));
            statuses.Add(response.StatusCode);
        }

        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, statuses.Take(3));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }
}

public class GlobalRateLimitTests(LowGlobalLimitFactory factory) : IClassFixture<LowGlobalLimitFactory>
{
    [Fact]
    public async Task TheGlobalLimit_CountsAnonymousApiRequests_ButNotStaticFiles()
    {
        // Anonymous requests to a protected endpoint: the limiter sits before authorization, so these
        // are counted rather than answered 401 indefinitely.
        var client = factory.CreateClient();
        HttpResponseMessage? last = null;
        for (var i = 0; i < 10; i++)
        {
            last = await client.GetAsync("/api/auth/me");
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        Assert.Contains("För många anrop", await last.Content.ReadAsStringAsync());

        // Not an API path, so not counted — the SPA's files must keep loading regardless.
        var page = await client.GetAsync("/favicon.ico");
        Assert.NotEqual(HttpStatusCode.TooManyRequests, page.StatusCode);
    }
}
