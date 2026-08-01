using System.Security.Claims;
using HouseApp.Api.Authorization;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Users;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HouseApp.Api.Controllers;

/// <summary>
/// Manages accounts. This container is **no longer an allowlist** — Google sign-in creates an account
/// for anyone who doesn't have one, because the app is published. So the meaningful power here is
/// IsBlocked (keep someone out) and IsAdmin (let someone manage), not the existence of the row:
/// deleting a user no longer revokes anything, since they'd get a fresh account on their next
/// sign-in. Still admin-only in full, listing included.
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize(Policy = Policies.Admin)]
public class UsersController(AppDbContext db) : ControllerBase
{
    private static readonly PasswordHasher<ApplicationUser> Hasher = new();

    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> GetAll()
    {
        var users = await db.Users.ToListAsync();
        return Ok(users.OrderBy(u => u.DisplayName).Select(ToDto));
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateUserRequest request)
    {
        var email = request.Email.Trim();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return BadRequest(new { message = "Email and display name are required." });
        }

        var existingUsers = await db.Users.ToListAsync();
        if (existingUsers.Any(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)))
        {
            return Conflict(new { message = "A user with that email already exists." });
        }

        var user = new ApplicationUser
        {
            Email = email,
            DisplayName = request.DisplayName.Trim(),
        };

        // Blank password => Google-only account. PasswordHash stays null and AuthController.Login
        // refuses it, so there's no "empty password logs in" hole.
        if (!string.IsNullOrWhiteSpace(request.InitialPassword))
        {
            user.PasswordHash = Hasher.HashPassword(user, request.InitialPassword);
        }

        db.Users.Add(user);

        // Deliberately no backfill onto existing properties. This used to add every new account to
        // every property, which was the whole sharing model back when there were two users and one
        // house; with several households it would hand a newcomer everyone else's finances. A new
        // account starts with nothing of its own and sees only the demo property, and is given
        // access to a real one by someone already on it.
        await db.SaveChangesAsync();
        return Ok(ToDto(user));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, UpdateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return BadRequest(new { message = "Display name is required." });
        }

        var user = await db.Users.FindAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (id == currentUserId && request.IsBlocked)
        {
            // Same family of guard as the two below: blocking yourself takes effect on the very next
            // request, so you'd be locked out mid-action with no way back in.
            return Conflict(new { message = "You can't block your own account." });
        }

        if (id == currentUserId && !request.IsAdmin)
        {
            // Keeps "at least one admin exists" true for good: the only account you can demote is
            // someone else's, and doing so requires you to still be an admin yourself. Combined
            // with the self-deletion guard below, the app can't be locked out of its own
            // administration.
            return Conflict(new { message = "You can't remove your own admin rights." });
        }

        user.DisplayName = request.DisplayName.Trim();
        user.IsAdmin = request.IsAdmin;
        user.IsBlocked = request.IsBlocked;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (id == currentUserId)
        {
            // The one genuinely destructive mistake available here — with no other admin surface,
            // deleting yourself while alone would lock everyone out of managing users.
            return Conflict(new { message = "You can't remove your own account." });
        }

        var user = await db.Users.FindAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        // Their id may linger in Property.MemberUserIds and the various *CreatedByUserId fields.
        // That's inert: membership only ever grants access to a user who can authenticate, and the
        // created-by ids are never displayed anywhere in the UI.
        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static UserDto ToDto(ApplicationUser u) =>
        new(u.Id, u.Email, u.DisplayName, !string.IsNullOrEmpty(u.PasswordHash), u.IsAdmin, u.IsBlocked, u.CreatedAt);
}
