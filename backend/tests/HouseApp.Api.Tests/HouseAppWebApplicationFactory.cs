using HouseApp.Api.Data;
using HouseApp.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HouseApp.Api.Tests;

/// <summary>
/// Boots the real API pipeline against an in-memory DB and a fake blob storage service, so
/// controller/auth/DTO wiring is exercised end-to-end without touching Cosmos or Azure Storage.
/// </summary>
public class HouseAppWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"houseapp-tests-{Guid.NewGuid()}";

    /// <summary>Exposed so tests can seed issues and inspect what the app filed.</summary>
    public FakeGitHubIssueService GitHub { get; } = new();

    /// <summary>
    /// Exposed so tests can assert what reached Drive and simulate a revoked grant. Registered as a
    /// singleton here even though the real service is scoped — the tests need one instance to look at.
    /// </summary>
    public FakeGoogleDriveService Drive { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Feedback caches the issue list for a couple of minutes in production, which would make
        // tests depend on each other's timing — seeding an issue directly wouldn't be visible until
        // the entry expired. Zero disables it.
        builder.UseSetting("Feedback:CacheSeconds", "0");

        builder.ConfigureServices(services =>
        {
            // AddDbContext registers its configuration as an IDbContextOptionsConfiguration<T> entry rather
            // than replacing one, so the app's Cosmos setup must be removed explicitly or both run and the
            // Cosmos one (which needs config that doesn't exist in tests) throws first.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<AppDbContext>));
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            services.RemoveAll<IBlobStorageService>();
            services.AddSingleton<IBlobStorageService, FakeBlobStorageService>();

            // A real OAuth round trip can't happen in-process, so only the outbound call to Google
            // is stubbed — the endpoint, allowlist check and cookie issuance are still the real ones.
            services.RemoveAll<IGoogleTokenValidator>();
            services.AddSingleton<IGoogleTokenValidator, FakeGoogleTokenValidator>();

            // Same treatment: the OAuth exchange and the Drive calls are stubbed, but the connect
            // controller, the protected state, the storage routing and the metadata writes are real.
            services.RemoveAll<IGoogleDriveService>();
            services.AddSingleton<IGoogleDriveService>(Drive);

            // Same treatment again: the label contract and visibility rules stay real, GitHub doesn't.
            services.RemoveAll<IGitHubIssueService>();
            services.AddSingleton<IGitHubIssueService>(GitHub);
        });
    }
}
