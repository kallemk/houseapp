using System.Security.Claims;
using HouseApp.Api.Data;
using HouseApp.Api.Dtos.Users;
using HouseApp.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HouseApp.Api.Controllers;

/// <summary>
/// Manages who may sign in: the users container doubles as the allowlist, so having a row here is
/// exactly what makes a Google account acceptable at /api/auth/google. Any signed-in user can
/// manage this — the app has no roles, and inventing one for two trusted people would be ceremony.
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize]
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

        // PropertiesController.Create only stamps the users that exist *at that moment* into
        // MemberUserIds, so someone added now would otherwise sign in to an empty property list.
        // Backfill them onto everything that already exists — consistent with this app's premise
        // that everyone shares every property.
        var properties = await db.Properties.ToListAsync();
        foreach (var property in properties)
        {
            property.MemberUserIds ??= [];
            if (!property.MemberUserIds.Contains(user.Id))
            {
                property.MemberUserIds.Add(user.Id);
            }
        }

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

        user.DisplayName = request.DisplayName.Trim();
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
        new(u.Id, u.Email, u.DisplayName, !string.IsNullOrEmpty(u.PasswordHash), u.CreatedAt);
}
