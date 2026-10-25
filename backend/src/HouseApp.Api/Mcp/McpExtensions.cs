using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;

namespace HouseApp.Api.Mcp;

public static class McpExtensions
{
    public const string Policy = "Mcp";

    /// <summary>
    /// The MCP server Claude connects to at /mcp, plus the token service behind its OAuth flow. The
    /// bearer scheme itself is registered with the cookie in AddHouseAppCookieAuth, because the
    /// choice between them is the default scheme.
    /// </summary>
    public static IServiceCollection AddHouseAppMcp(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddHttpContextAccessor();
        services.AddSingleton<McpTokenService>();

        services.AddAuthorizationBuilder().AddPolicy(Policy, policy => policy
            .AddAuthenticationSchemes(McpBearerHandler.SchemeName)
            .RequireAuthenticatedUser());

        services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation { Name = "HusTracker", Version = "1.0" };
                options.ServerInstructions =
                    "HusTracker keeps a household's record of their house: projects (maintenance, renovation, " +
                    "investment, purchases), their costs, a maintenance schedule, budgets and documents. Start with " +
                    "list_properties. The user writes in Swedish; amounts are SEK. Nothing can be deleted through " +
                    "these tools — point the user to the app for that. Files can't be uploaded here: use " +
                    "create_upload_link and give the user the link.";
            })
            // Stateless: no session to lose when App Service restarts on every deploy, and nothing to
            // pin to an instance.
            .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
            .WithTools<HouseTools>();

        return services;
    }
}
