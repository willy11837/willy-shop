using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Contracts;
using WebApplication1.Models;

namespace WebApplication1.Controllers.Api.V1;

[ApiController]
[Authorize(Roles = SystemRoles.AccountManagers)]
[Route("api/v1/admin/users")]
public sealed class AdminUsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminUsersController(UserManager<ApplicationUser> userManager) => _userManager = userManager;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminUserDto>>> GetAll()
    {
        var users = await _userManager.Users.OrderBy(user => user.UserName).Take(100).ToListAsync();
        var result = new List<AdminUserDto>();
        foreach (var user in users)
            result.Add(await MapAsync(user));
        return Ok(result);
    }

    [HttpGet("roles")]
    public ActionResult<IReadOnlyList<string>> GetRoles() => Ok(AvailableRoles());

    [ValidateAntiForgeryToken]
    [HttpPost]
    public async Task<ActionResult<AdminUserDto>> Create(CreateUserRequest request)
    {
        if (!AvailableRoles().Contains(request.Role)) return Forbid();
        if (await _userManager.FindByNameAsync(request.Account.Trim()) is not null)
            return Conflict(Problem("帳號已存在。", statusCode: 409));
        if (await _userManager.FindByEmailAsync(request.Email.Trim()) is not null)
            return Conflict(Problem("Email 已存在。", statusCode: 409));

        var user = new ApplicationUser
        {
            UserName = request.Account.Trim(),
            DisplayName = request.DisplayName.Trim(),
            Email = request.Email.Trim(),
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            ModifiedBy = User.Identity?.Name
        };
        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded) return IdentityErrors(result);
        var roleResult = await _userManager.AddToRoleAsync(user, request.Role);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return IdentityErrors(roleResult);
        }
        return Ok(await MapAsync(user));
    }

    [ValidateAntiForgeryToken]
    [HttpPut("{id}")]
    public async Task<ActionResult<AdminUserDto>> Update(string id, UpdateUserRequest request)
    {
        if (!AvailableRoles().Contains(request.Role)) return Forbid();
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();
        var oldRoles = await _userManager.GetRolesAsync(user);
        if (!User.IsInRole(SystemRoles.System) && oldRoles.Contains(SystemRoles.System)) return Forbid();
        if (oldRoles.Contains(SystemRoles.System) && request.Role != SystemRoles.System && (await _userManager.GetUsersInRoleAsync(SystemRoles.System)).Count <= 1)
            return BadRequest(Problem("不可變更最後一個 System 帳號的權限。", statusCode: 400));
        var emailOwner = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (emailOwner is not null && emailOwner.Id != id) return Conflict(Problem("Email 已被使用。", statusCode: 409));

        user.DisplayName = request.DisplayName.Trim();
        user.Email = request.Email.Trim();
        user.ModifiedBy = User.Identity?.Name;
        user.ModifiedAt = DateTime.UtcNow;
        var update = await _userManager.UpdateAsync(user);
        if (!update.Succeeded) return IdentityErrors(update);
        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var passwordResult = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);
            if (!passwordResult.Succeeded) return IdentityErrors(passwordResult);
        }
        if (oldRoles.Count > 0) await _userManager.RemoveFromRolesAsync(user, oldRoles);
        var roleResult = await _userManager.AddToRoleAsync(user, request.Role);
        if (!roleResult.Succeeded) return IdentityErrors(roleResult);
        return Ok(await MapAsync(user));
    }

    [Authorize(Roles = SystemRoles.System)]
    [ValidateAntiForgeryToken]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var current = await _userManager.GetUserAsync(User);
        if (current?.Id == id) return BadRequest(Problem("不可刪除目前登入的帳號。", statusCode: 400));
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();
        if (await _userManager.IsInRoleAsync(user, SystemRoles.System) && (await _userManager.GetUsersInRoleAsync(SystemRoles.System)).Count <= 1)
            return BadRequest(Problem("不可刪除最後一個 System 帳號。", statusCode: 400));
        var result = await _userManager.DeleteAsync(user);
        return result.Succeeded ? NoContent() : IdentityErrors(result);
    }

    private List<string> AvailableRoles() => User.IsInRole(SystemRoles.System)
        ? [.. SystemRoles.All]
        : [SystemRoles.Admin, SystemRoles.Store, SystemRoles.Normal, SystemRoles.Guest];

    private async Task<AdminUserDto> MapAsync(ApplicationUser user)
    {
        var role = (await _userManager.GetRolesAsync(user)).FirstOrDefault() ?? SystemRoles.Guest;
        return new AdminUserDto(user.Id, user.UserName ?? string.Empty, user.DisplayName, user.Email, role,
            user.CreatedAt, user.ModifiedBy, user.ModifiedAt);
    }

    private ActionResult IdentityErrors(IdentityResult result) => BadRequest(new ValidationProblemDetails(
        result.Errors.GroupBy(error => error.Code).ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray())));
}