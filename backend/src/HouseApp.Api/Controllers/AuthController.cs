using System.Security.Claims;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Auth;
using HouseApp.Api.Models;
using HouseApp.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HouseApp.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Authorize]
public class AuthController(AppDbContext db, IGoogleTokenValidator googleTokenValidator) : ControllerBase
{
    private static readonly PasswordHasher<ApplicationUser> Hasher = new();

    // No self-registration endpoint on purpose — accounts are created either by DbSeeder
    // (bootstrap) or by an existing user via UsersController.

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<MeResponse>> Login(LoginRequest request)
    {
        // .Where(...).ToListAsync() rather than SingleOrDefaultAsync(predicate) — the Cosmos
        // provider's SQL generation for predicate-based Single/Any/First is unreliable.
        var user = (await db.Users.Where(u => u.Email == request.Email).ToListAsync()).SingleOrDefault();
        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
        {
            // No password set means this is a Google-only account — same generic 401 as a wrong
            // password, so this endpoint can't be used to probe which accounts exist.
            return Unauthorized();
        }

        var result = Hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            return Unauthorized();
        }

        await SignInAsync(user);
        return Ok(new MeResponse(user.Id, user.Email, user.DisplayName, user.IsAdmin));
    }

    /// <summary>
    /// Exchanges a Google ID token for the app's own session cookie. Deliberately does NOT create
    /// users: the users container doubles as the sign-in allowlist, and matching an existing row by
    /// email is what preserves ApplicationUser.Id — the id already stored in Property.MemberUserIds
    /// and every *CreatedByUserId. Minting a new id here would orphan all of it.
    /// </summary>
    [HttpPost("google")]
    [AllowAnonymous]
    public async Task<ActionResult<MeResponse>> GoogleLogin(GoogleLoginRequest request)
    {
        if (!googleTokenValidator.IsConfigured)
        {
            // A deployment problem, not an auth failure — returning 401 here would send everyone
            // hunting for a bad token when the real fix is setting the client ID app setting.
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new { message = "Google sign-in is not configured on the server." });
        }

        var googleUser = await googleTokenValidator.ValidateAsync(request.Credential);
        if (googleUser is null || !googleUser.EmailVerified)
        {
            return Unauthorized();
        }

        // Whole-container read then in-memory match: the users container is tiny, this matches the
        // pattern DbSeeder already uses, and it lets the comparison be case-insensitive without
        // relying on Cosmos query casing semantics.
        var user = (await db.Users.ToListAsync())
            .SingleOrDefault(u => string.Equals(u.Email, googleUser.Email, StringComparison.OrdinalIgnoreCase));

        if (user is null)
        {
            // 403 rather than 401: the Google account is genuine, it just isn't on the allowlist.
            // The frontend distinguishes these to show "not invited" vs "sign-in failed".
            // StatusCode() rather than Forbid() — the latter routes through the cookie handler's
            // forbid path, and this is a plain JSON API response.
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        if (string.IsNullOrEmpty(user.GoogleSubjectId))
        {
            user.GoogleSubjectId = googleUser.Subject;
            await db.SaveChangesAsync();
        }

        await SignInAsync(user);
        return Ok(new MeResponse(user.Id, user.Email, user.DisplayName, user.IsAdmin));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<ActionResult<MeResponse>> Me()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await db.Users.FindAsync(userId);
        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(new MeResponse(user.Id, user.Email, user.DisplayName, user.IsAdmin));
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await db.Users.FindAsync(userId);
        if (user is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            return BadRequest(new { message = "This account has no password. Ask another user to set one for you." });
        }

        var result = Hasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
        if (result == PasswordVerificationResult.Failed)
        {
            return BadRequest(new { message = "Current password is incorrect." });
        }

        user.PasswordHash = Hasher.HashPassword(user, request.NewPassword);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task SignInAsync(ApplicationUser user)
    {
        // NameIdentifier must stay ApplicationUser.Id (never Google's subject) — it's the id stored
        // across properties/valuations/renovations/documents.
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        // IsPersistent is load-bearing and must not be dropped. Without it the handler writes
        // Set-Cookie with no Expires/Max-Age — a *session* cookie the browser may discard whenever
        // the browsing session ends. ExpireTimeSpan/SlidingExpiration in AddHouseAppCookieAuth do
        // NOT cover this: they bound the ticket encrypted inside the cookie, not how long the
        // browser keeps it. Without this line the server believes it is issuing 14-day sliding
        // sessions while Android Chrome — which the OS kills routinely, and which has no desktop
        // "continue where you left off" to restore session cookies — logs people out constantly.
        var properties = new AuthenticationProperties { IsPersistent = true };
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            properties);
    }
}
