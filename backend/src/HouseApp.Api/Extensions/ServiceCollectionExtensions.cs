using System.Security.Claims;
using Azure.Identity;
using Azure.Storage;
using Azure.Storage.Blobs;
using HouseApp.Api.Authorization;
using HouseApp.Api.Data;
using HouseApp.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
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

                // Blocking has to take effect now, not in up to 14 days. The cookie carries a
                // 14-day sliding session, so without this a blocked account keeps working until it
                // happens to expire — which makes "blocked" nearly meaningless as a way to remove
                // someone. Same argument as AdminAuthorizationHandler reading IsAdmin from the
                // database rather than a claim, and it matters more here: that one governs extra
                // powers, this one governs access at all.
                //
                // The cost is a point read per authenticated request. On Cosmos that's ~1 RU and a
                // couple of milliseconds, which is the right trade for a revocation that works.
                options.Events.OnValidatePrincipal = async context =>
                {
                    var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (userId is null)
                    {
                        return;
                    }

                    var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                    var user = await db.Users.FindAsync(userId);

                    // Deleted accounts are rejected here too, which is what makes deletion take
                    // effect immediately rather than leaving a live session behind it.
                    if (user is null || user.IsBlocked)
                    {
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    }
                };

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
        // Scoped, not singleton: the handler resolves AppDbContext from the request scope so it can
        // check IsAdmin against the database rather than a claim (see AdminAuthorizationHandler).
        // A failed policy lands on OnRedirectToAccessDenied above, so callers get a clean 403.
        services.AddScoped<IAuthorizationHandler, AdminAuthorizationHandler>();
        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Admin, policy => policy.AddRequirements(new AdminRequirement()));

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

    /// <summary>
    /// Google Drive as an alternative document store, opt-in per property.
    ///
    /// This *is* a redirect-based OAuth flow, unlike sign-in above — Drive has no equivalent of the
    /// ID-token shortcut, since the app needs a long-lived grant to act on the user's behalf rather
    /// than a one-off proof of who they are. It stays compatible with the SameSite=Lax cookie because
    /// the redirect is a top-level navigation, and the callback lands on the same public origin.
    /// </summary>
    public static IServiceCollection AddHouseAppGoogleDrive(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(GoogleDriveService));
        services.AddSingleton<IDriveTokenProtector, DriveTokenProtector>();
        services.AddScoped<IGoogleDriveService, GoogleDriveService>();
        // Scoped: both resolve AppDbContext — one to find the connecting user's token, the other to
        // read and record the folder ids it creates.
        services.AddScoped<IDriveAccessTokenResolver, DriveAccessTokenResolver>();
        services.AddScoped<IDriveFolderResolver, DriveFolderResolver>();
        return services;
    }
}
