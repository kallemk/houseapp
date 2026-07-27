using Azure.Identity;
using Azure.Storage;
using Azure.Storage.Blobs;
using HouseApp.Api.Data;
using HouseApp.Api.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace HouseApp.Api.Extensions;

public static class ServiceCollectionExtensions
{
    // The SDK's default ("Auto") download transfer validation negotiates structured-message/CRC64
    // downloads, which this storage account/region combination rejects with a malformed "comp"
    // query parameter (InvalidQueryParameterValue) — this crashed both DbSeeder and cookie sign-in
    // (Data Protection reads its key ring from Blob Storage on every request). Disabling it avoids
    // the negotiation entirely; SAS-issued URLs for document upload/download are unaffected since
    // those are plain HTTP PUT/GET from the browser, not SDK calls.
    private static BlobClientOptions CreateBlobClientOptions() => new()
    {
        TransferValidation = { Download = { ChecksumAlgorithm = StorageChecksumAlgorithm.None } },
    };

    public static IServiceCollection AddHouseAppCookieAuth(this IServiceCollection services)
    {
        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "houseapp.auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax; // safe because SWA proxies the API same-origin
                // Mirrors the request scheme rather than forcing Secure: prod is always https (SWA/App
                // Service), while local dev/tests run plain http and would otherwise never see the cookie
                // come back (HttpClient/browsers withhold Secure cookies from non-https requests).
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = TimeSpan.FromDays(14);
                options.SlidingExpiration = true;

                // This is a JSON API — return status codes instead of redirecting to a login page.
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });
        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddHouseAppDataProtection(this IServiceCollection services)
    {
        // Persisted to local disk, not Blob Storage — App Service Linux's /home directory is
        // persistent across restarts/idle-unloads for a single instance (our F1 plan runs exactly
        // one), and this keeps the Blob SDK out of the startup/login path entirely for something as
        // foundational as the auth cookie key ring. HOME is set to /home on App Service Linux; local
        // dev falls back to the OS temp directory.
        //
        // Not encrypted-at-rest with Key Vault: ProtectKeysWithAzureKeyVault needs a URI to a
        // specific key inside the vault (.../keys/<name>), not the vault's own base URI, so doing
        // this properly means provisioning a Key Vault key via Bicep too. Skipped as unnecessary
        // hardening for a 2-user app — the key file is only readable by the App Service's own
        // process/identity either way.
        var keysPath = Environment.GetEnvironmentVariable("HOME") is { } home
            ? Path.Combine(home, "data-protection-keys")
            : Path.Combine(Path.GetTempPath(), "houseapp-dataprotection-keys");
        Directory.CreateDirectory(keysPath);

        services.AddDataProtection()
            .SetApplicationName("HouseApp")
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath));

        return services;
    }

    public static IServiceCollection AddHouseAppData(this IServiceCollection services, IConfiguration configuration)
    {
        var cosmosConnectionString = configuration.GetConnectionString("Cosmos");
        var databaseName = configuration["Cosmos:DatabaseName"] ?? "houseapp";

        // Created once here, not inside the options lambda below: AddDbContext is Scoped, so that
        // lambda re-runs on every request (once per DI scope). A fresh DefaultAzureCredential
        // instance each time defeats EF Core's internal service-provider cache (it's part of the
        // cache key), which eventually throws ManyServiceProvidersCreatedWarning after 20 requests.
        var credential = new DefaultAzureCredential();

        services.AddDbContext<AppDbContext>(options =>
        {
            if (!string.IsNullOrEmpty(cosmosConnectionString))
            {
                options.UseCosmos(cosmosConnectionString, databaseName);
            }
            else
            {
                var accountEndpoint = configuration["Cosmos:AccountEndpoint"]
                    ?? throw new InvalidOperationException("Either ConnectionStrings:Cosmos or Cosmos:AccountEndpoint must be configured.");
                options.UseCosmos(accountEndpoint, credential, databaseName);
            }
        });

        return services;
    }

    public static IServiceCollection AddHouseAppBlobStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(_ =>
        {
            var storageConnectionString = configuration.GetConnectionString("Storage");
            if (!string.IsNullOrEmpty(storageConnectionString))
            {
                return new BlobServiceClient(storageConnectionString, CreateBlobClientOptions());
            }

            var storageAccountUrl = configuration["Storage:AccountUrl"]
                ?? throw new InvalidOperationException("Either ConnectionStrings:Storage or Storage:AccountUrl must be configured.");
            return new BlobServiceClient(new Uri(storageAccountUrl), new DefaultAzureCredential(), CreateBlobClientOptions());
        });
        services.AddScoped<IBlobStorageService, BlobStorageService>();
        return services;
    }

    public static IServiceCollection AddHouseAppGoogleAuth(this IServiceCollection services)
    {
        // No AddGoogle()/OAuth handler here on purpose: the frontend uses Google Identity Services
        // to obtain an ID token and posts it to /api/auth/google, so there is no redirect dance —
        // which is what lets the session stay a plain same-origin cookie (see CLAUDE.md).
        services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();
        return services;
    }
}
