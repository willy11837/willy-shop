using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WillyShop.Contracts;
using WillyShop.Models;

namespace WillyShop.Controllers.Api.V1;

[ApiController]
[Authorize]
[Route("api/v1/profile")]
public sealed class ProfileController : ApiControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ProfileController(UserManager<ApplicationUser> userManager) => _userManager = userManager;

    [HttpGet]
    public async Task<ActionResult<ProfileDto>> Get()
    {
        var user = await _userManager.GetUserAsync(User);
        return user is null
            ? ApiFailure("需要有效的登入憑證。", StatusCodes.Status401Unauthorized)
            : ApiSuccess(await MapAsync(user));
    }
    [HttpPut]
    public async Task<ActionResult<ProfileDto>> Update(UpdateProfileRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return ApiFailure("需要有效的登入憑證。", StatusCodes.Status401Unauthorized);

        var email = request.Email.Trim();
        var emailOwner = await _userManager.FindByEmailAsync(email);
        if (emailOwner is not null && emailOwner.Id != user.Id)
            return ApiFailure("此電子郵件已被使用。", StatusCodes.Status409Conflict);

        user.DisplayName = request.DisplayName.Trim();
        user.Email = email;
        user.ModifiedBy = user.UserName;
        user.ModifiedAt = DateTime.UtcNow;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return ApiValidationFailure(result.Errors.GroupBy(error => error.Code).ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Description).ToArray()));

        return ApiSuccess(await MapAsync(user), "個人資料更新成功。");
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return ApiFailure("需要有效的登入憑證。", StatusCodes.Status401Unauthorized);

        user.ModifiedBy = user.UserName;
        user.ModifiedAt = DateTime.UtcNow;
        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return ApiValidationFailure(result.Errors.GroupBy(error => error.Code).ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Description).ToArray()));

        return ApiSuccess<object?>(null, "密碼修改成功。");
    }

    private async Task<ProfileDto> MapAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return new ProfileDto(
            user.Id,
            user.UserName ?? string.Empty,
            user.DisplayName ?? string.Empty,
            user.Email ?? string.Empty,
            roles.ToList(),
            user.CreatedAt,
            user.ModifiedAt);
    }
}