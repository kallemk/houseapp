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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

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
        });
    }
}
