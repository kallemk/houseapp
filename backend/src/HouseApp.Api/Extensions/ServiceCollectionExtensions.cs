using Azure.Identity;
using Azure.Storage.Blobs;
using HouseApp.Api.Data;
using HouseApp.Api.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace HouseApp.Api.Extensions;

public static class ServiceCollectionExtensions
{
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

    public static IServiceCollection AddHouseAppDataProtection(this IServiceCollection services, IConfiguration configuration)
    {
        var storageConnectionString = configuration.GetConnectionString("Storage");
        var keyVaultUri = configuration["KeyVault:Uri"];

        var builder = services.AddDataProtection().SetApplicationName("HouseApp");

        if (!string.IsNullOrEmpty(storageConnectionString))
        {
            var containerClient = new BlobContainerClient(storageConnectionString, "dataprotection-keys");
            containerClient.CreateIfNotExists();
            builder.PersistKeysToAzureBlobStorage(containerClient.GetBlobClient("keys.xml"));
        }
        else
        {
            var storageAccountUrl = configuration["Storage:AccountUrl"]
                ?? throw new InvalidOperationException("Either ConnectionStrings:Storage or Storage:AccountUrl must be configured.");
            var credential = new DefaultAzureCredential();
            var containerClient = new BlobContainerClient(new Uri($"{storageAccountUrl}/dataprotection-keys"), credential);
            containerClient.CreateIfNotExists();
            builder.PersistKeysToAzureBlobStorage(containerClient.GetBlobClient("keys.xml"));

            if (!string.IsNullOrEmpty(keyVaultUri))
            {
                builder.ProtectKeysWithAzureKeyVault(new Uri(keyVaultUri), credential);
            }
        }

        return services;
    }

    public static IServiceCollection AddHouseAppData(this IServiceCollection services, IConfiguration configuration)
    {
        var cosmosConnectionString = configuration.GetConnectionString("Cosmos");
        var databaseName = configuration["Cosmos:DatabaseName"] ?? "houseapp";

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
                options.UseCosmos(accountEndpoint, new DefaultAzureCredential(), databaseName);
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
                return new BlobServiceClient(storageConnectionString);
            }

            var storageAccountUrl = configuration["Storage:AccountUrl"]
                ?? throw new InvalidOperationException("Either ConnectionStrings:Storage or Storage:AccountUrl must be configured.");
            return new BlobServiceClient(new Uri(storageAccountUrl), new DefaultAzureCredential());
        });
        services.AddScoped<IBlobStorageService, BlobStorageService>();
        return services;
    }
}
