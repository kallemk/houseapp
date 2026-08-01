using System.Net;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace HouseApp.Api.Tests;

/// <summary>
/// Boots the app with a stand-in SPA in wwwroot, so the fallback rules are exercised the way they
/// will be in production. The base factory deliberately has no wwwroot — that's what keeps every
/// other test unaffected by SPA hosting.
/// </summary>
public class SpaHostingFactory : HouseAppWebApplicationFactory
{
    private readonly string _webRoot = Path.Combine(Path.GetTempPath(), $"houseapp-spa-{Guid.NewGuid()}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(Path.Combine(_webRoot, "assets"));
        File.WriteAllText(Path.Combine(_webRoot, "index.html"), "<!doctype html><title>HusTracker</title>");
        File.WriteAllText(Path.Combine(_webRoot, "assets", "app.js"), "console.log('hello')");

        builder.UseWebRoot(_webRoot);
        base.ConfigureWebHost(builder);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_webRoot))
        {
            Directory.Delete(_webRoot, recursive: true);
        }
    }
}

/// <summary>
/// The App Service serves the SPA itself now that the Static Web App is gone, which moves three
/// rules out of staticwebapp.config.json and into Program.cs. All three were load-bearing there and
/// none of them is obvious from reading the routing code, so they're pinned here.
/// </summary>
public class SpaFallbackTests : IClassFixture<SpaHostingFactory>
{
    private readonly SpaHostingFactory _factory;

    public SpaFallbackTests(SpaHostingFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ADeepLink_ServesTheSpa_SoARefreshWorks()
    {
        // /properties/<id>/documents is a client-side route, not a file. Without the fallback the
        // server 404s a URL the app considers perfectly valid — which is what "it works until you
        // press F5" looks like.
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/properties/abc123/documents");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("HusTracker", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task TheSpaIsServedWithoutSigningIn()
    {
        // The shell has to load for anyone — it *is* the login page. If [Authorize] ever leaked onto
        // the fallback, the app would be unreachable rather than merely locked.
        var response = await _factory.CreateClient().GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AnUnknownApiPath_Returns404_NotTheSpa()
    {
        // Handing HTML back from an API path turns a plain "no such endpoint" into a JSON parse
        // error somewhere far away from the cause.
        var response = await _factory.CreateClient().GetAsync("/api/nope");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task AMissingAsset_Returns404_NotTheSpa()
    {
        // A stale hashed bundle must fail as a missing script. Served as HTML under a .js URL it
        // surfaces as a baffling MIME-type error instead of an obvious 404.
        var response = await _factory.CreateClient().GetAsync("/assets/index-stale123.js");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ARealAssetIsStillServed()
    {
        var response = await _factory.CreateClient().GetAsync("/assets/app.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/javascript", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task TheApiStillWorksAlongsideTheSpa()
    {
        // The fallback sits behind MapControllers; a route that exists must still reach its
        // controller rather than being swallowed by the SPA.
        var response = await _factory.CreateClient().GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
