using System.Security.Claims;

namespace HouseApp.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The signed-in ApplicationUser.Id. Non-null by construction: every action reaching this is
    /// behind [Authorize], and AuthController.SignInAsync always writes this claim (never Google's
    /// subject — see the note there).
    /// </summary>
    public static string CurrentUserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)!;
}
