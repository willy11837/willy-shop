using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WillyShop.Contracts;
using WillyShop.Data;
using WillyShop.Logging;
using WillyShop.Models;

namespace WillyShop.Controllers.Api.V1;

[ApiController]
[Authorize(Roles = SystemRoles.AccountManagers)]
[Route("api/v1/admin/users")]
public sealed class AdminUsersController : ApiControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AdminUsersController> _logger;

    public AdminUsersController(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db,
        ILogger<AdminUsersController> logger)
    {
        _userManager = userManager;
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminUserDto>>> GetAll(
        [FromQuery] AdminUserQuery query,
        CancellationToken cancellationToken)
    {
        var users = _userManager.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            users = users.Where(user => (user.UserName != null && user.UserName.Contains(keyword))
                || (user.DisplayName != null && user.DisplayName.Contains(keyword))
                || (user.Email != null && user.Email.Contains(keyword)));
        }

        var totalCount = await users.CountAsync(cancellationToken);
        var pageUsers = await users.OrderBy(user => user.UserName)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);
        var ids = pageUsers.Select(user => user.Id).ToList();
        var roleMap = await (from userRole in _db.UserRoles.AsNoTracking()
                             join role in _db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                             where ids.Contains(userRole.UserId)
                             select new { userRole.UserId, Role = role.Name })
            .ToDictionaryAsync(value => value.UserId, value => value.Role ?? SystemRoles.Guest, cancellationToken);
        var result = pageUsers.Select(user => Map(user, roleMap.GetValueOrDefault(user.Id, SystemRoles.Guest))).ToList();
        return ApiSuccess(new PagedResult<AdminUserDto>(result, query.Page, query.PageSize, totalCount));
    }

    [HttpGet("roles")]
    public ActionResult<IReadOnlyList<string>> GetRoles() => ApiSuccess(AvailableRoles());
    [HttpPost]
    public async Task<ActionResult<AdminUserDto>> Create(CreateUserRequest request)
    {
        if (!AvailableRoles().Contains(request.Role)) return ApiFailure("目前帳號無法指派此角色。", StatusCodes.Status403Forbidden);
        if (await _userManager.FindByNameAsync(request.Account.Trim()) is not null)
            return ApiFailure("帳號已存在。", StatusCodes.Status409Conflict);
        if (await _userManager.FindByEmailAsync(request.Email.Trim()) is not null)
            return ApiFailure("Email 已存在。", StatusCodes.Status409Conflict);

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
        _logger.AdministrativeUserCreated(user.Id, request.Role, ActorUserId());
        return ApiSuccess(await MapAsync(user), "使用者建立成功。", StatusCodes.Status201Created);
    }
    [HttpPut("{id}")]
    public async Task<ActionResult<AdminUserDto>> Update(string id, UpdateUserRequest request)
    {
        if (!AvailableRoles().Contains(request.Role)) return ApiFailure("目前帳號無法指派此角色。", StatusCodes.Status403Forbidden);
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return ApiFailure("找不到指定的使用者。", StatusCodes.Status404NotFound);
        var oldRoles = await _userManager.GetRolesAsync(user);
        if (!User.IsInRole(SystemRoles.System) && oldRoles.Contains(SystemRoles.System))
            return ApiFailure("目前帳號無法修改 System 使用者。", StatusCodes.Status403Forbidden);
        if (oldRoles.Contains(SystemRoles.System) && request.Role != SystemRoles.System && (await _userManager.GetUsersInRoleAsync(SystemRoles.System)).Count <= 1)
            return ApiFailure("不可移除最後一個 System 帳號的系統角色。", StatusCodes.Status400BadRequest);
        var emailOwner = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (emailOwner is not null && emailOwner.Id != id) return ApiFailure("Email 已被使用。", StatusCodes.Status409Conflict);

        user.DisplayName = request.DisplayName.Trim();
        user.Email = request.Email.Trim();
        user.ModifiedBy = User.Identity?.Name;
        user.ModifiedAt = DateTime.UtcNow;
        var update = await _userManager.UpdateAsync(user);
        if (!update.Succeeded) return IdentityErrors(update);
        var passwordChanged = !string.IsNullOrWhiteSpace(request.NewPassword);
        if (passwordChanged)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var passwordResult = await _userManager.ResetPasswordAsync(user, token, request.NewPassword!);
            if (!passwordResult.Succeeded) return IdentityErrors(passwordResult);
        }
        if (oldRoles.Count > 0)
        {
            var removeRolesResult = await _userManager.RemoveFromRolesAsync(user, oldRoles);
            if (!removeRolesResult.Succeeded) return IdentityErrors(removeRolesResult);
        }
        var roleResult = await _userManager.AddToRoleAsync(user, request.Role);
        if (!roleResult.Succeeded) return IdentityErrors(roleResult);
        _logger.AdministrativeUserUpdated(user.Id, request.Role, ActorUserId(), passwordChanged);
        return ApiSuccess(await MapAsync(user), "使用者更新成功。");
    }

    [Authorize(Roles = SystemRoles.System)]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var current = await _userManager.GetUserAsync(User);
        if (current?.Id == id) return ApiFailure("不可刪除目前登入的帳號。", StatusCodes.Status400BadRequest);
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return ApiFailure("找不到指定的使用者。", StatusCodes.Status404NotFound);
        if (await _userManager.IsInRoleAsync(user, SystemRoles.System) && (await _userManager.GetUsersInRoleAsync(SystemRoles.System)).Count <= 1)
            return ApiFailure("不可刪除最後一個 System 帳號。", StatusCodes.Status400BadRequest);
        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded) return IdentityErrors(result);
        _logger.AdministrativeUserDeleted(user.Id, ActorUserId());
        return ApiSuccess<object?>(null, "使用者刪除成功。");
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

    private static AdminUserDto Map(ApplicationUser user, string role) => new(
        user.Id, user.UserName ?? string.Empty, user.DisplayName, user.Email, role,
        user.CreatedAt, user.ModifiedBy, user.ModifiedAt);

    private ActionResult IdentityErrors(IdentityResult result) => ApiValidationFailure(
        result.Errors.GroupBy(error => error.Code).ToDictionary(
            group => group.Key,
            group => group.Select(error => error.Description).ToArray()));

    private string? ActorUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
}