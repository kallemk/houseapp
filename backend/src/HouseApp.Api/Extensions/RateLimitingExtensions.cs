using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace HouseApp.Api.Extensions;

/// <summary>Requests allowed per window. Bound from <c>RateLimits:*</c>; the tests raise them.</summary>
public class RateLimitSettings
{
    /// <summary>Every /api request, per signed-in user (or per IP before sign-in). A page load is ~10.</summary>
    public int RequestsPerMinute { get; set; } = 300;

    /// <summary>Sign-in attempts per IP — what slows down password guessing.</summary>
    public int SignInsPerMinute { get; set; } = 10;

    public int ExportsPerHour { get; set; } = 10;

    /// <summary>upload-url and the Drive upload together. Each SAS URL can carry a file of any size.</summary>
    public int UploadsPerHour { get; set; } = 60;
}

/// <summary>
/// Cosmos is serverless — every request is billed — and registration is open, so request volume is
/// capped per caller. Built on ASP.NET Core's own rate limiter; no package.
/// </summary>
public static class RateLimitingExtensions
{
    public const string SignInPolicy = "sign-in";
    public const string ExportPolicy = "export";
    public const string UploadPolicy = "upload";

    public static IServiceCollection AddHouseAppRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimitSettings>().BindConfiguration("RateLimits");

        // App Service terminates TLS in a front end that appends the caller's address to
        // X-Forwarded-For. Without this every request looks like it comes from that front end, and the
        // per-IP sign-in limit would be one bucket for the whole internet. ForwardLimit = 1 takes only
        // the address the front end added — anything a client put in the header itself is ignored.
        // The front end's own addresses aren't published, hence no known proxies to check against.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                }

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { message = "För många anrop – vänta en stund och försök igen." },
                    cancellationToken);
            };

            // Only the API: the SPA's scripts and images are static files and cost nothing.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                context.Request.Path.StartsWithSegments("/api")
                    ? FixedWindow(CallerKey(context), Settings(context).RequestsPerMinute, TimeSpan.FromMinutes(1))
                    : RateLimitPartition.GetNoLimiter("static"));

            options.AddPolicy(SignInPolicy, context =>
                FixedWindow($"ip:{IpOf(context)}", Settings(context).SignInsPerMinute, TimeSpan.FromMinutes(1)));
            options.AddPolicy(ExportPolicy, context =>
                FixedWindow(CallerKey(context), Settings(context).ExportsPerHour, TimeSpan.FromHours(1)));
            options.AddPolicy(UploadPolicy, context =>
                FixedWindow(CallerKey(context), Settings(context).UploadsPerHour, TimeSpan.FromHours(1)));
        });

        return services;
    }

    private static RateLimitSettings Settings(HttpContext context) =>
        context.RequestServices.GetRequiredService<IOptions<RateLimitSettings>>().Value;

    /// <summary>
    /// The user once signed in — so a household behind one router doesn't share a bucket — and the
    /// address before that. Needs UseRateLimiter after UseAuthentication to see the user.
    /// </summary>
    private static string CallerKey(HttpContext context) =>
        context.User.FindFirstValue(ClaimTypes.NameIdentifier) is { } userId ? $"user:{userId}" : $"ip:{IpOf(context)}";

    private static string IpOf(HttpContext context) => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static RateLimitPartition<string> FixedWindow(string key, int permits, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permits,
            Window = window,
            QueueLimit = 0,
        });
}
