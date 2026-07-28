using System.Security.Claims;
using HouseApp.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace HouseApp.Api.Authorization;

public class AdminRequirement : IAuthorizationRequirement;

/// <summary>
/// Backs the "Admin" policy by reading ApplicationUser.IsAdmin from the database on each check,
/// rather than trusting a role claim baked into the auth cookie at sign-in.
///
/// That's deliberate: the cookie lives 14 days with sliding expiration, so a claim-based check
/// would leave a demoted user with admin powers for up to two weeks, and a promotion wouldn't take
/// effect until the person logged out and back in. This is a point read (~1 RU) and only runs on
/// the handful of admin-gated endpoints — AuthController.Me already does the same lookup per call.
/// </summary>
public class AdminAuthorizationHandler(AppDbContext db) : AuthorizationHandler<AdminRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminRequirement requirement)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return;
        }

        // FindAsync is a point read by id (the partition key on the users container), not a
        // predicate query — safe on the Cosmos provider.
        var user = await db.Users.FindAsync(userId);
        if (user is { IsAdmin: true })
        {
            context.Succeed(requirement);
        }
    }
}

public static class Policies
{
    public const string Admin = "Admin";
}
