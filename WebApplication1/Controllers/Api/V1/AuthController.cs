using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Contracts;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers.Api.V1;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly CartService _cartService;
    private readonly IAntiforgery _antiforgery;

    public AuthController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        CartService cartService,
        IAntiforgery antiforgery)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _cartService = cartService;
        _antiforgery = antiforgery;
    }

    [AllowAnonymous]
    [HttpGet("csrf-token")]
    public ActionResult<object> GetCsrfToken()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { token = tokens.RequestToken });
    }

    [AllowAnonymous]
    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserDto>> Me()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
            return Ok(new CurrentUserDto(false, null, null, null, null, [SystemRoles.Guest]));
        return Ok(await MapUserAsync(user));
    }

    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [HttpPost("login")]
    public async Task<ActionResult<CurrentUserDto>> Login(LoginRequest request)
    {
        var account = request.Account.Trim();
        var result = await _signInManager.PasswordSignInAsync(account, request.Password, request.RememberMe, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            var message = result.IsLockedOut ? "帳號已暫時鎖定，請稍後再試。" : "登入失敗，帳號或密碼錯誤。";
            return Unauthorized(new ProblemDetails { Title = "登入失敗", Detail = message, Status = StatusCodes.Status401Unauthorized });
        }

        var user = await _userManager.FindByNameAsync(account);
        if (user is null) return Unauthorized();
        var anonymousCart = _cartService.GetAnonymousCart(HttpContext);
        if (anonymousCart.Count > 0)
        {
            await _cartService.MergeAnonymousCartToUserAsync(user.Id, anonymousCart);
            _cartService.ClearAnonymousCart(HttpContext);
        }
        return Ok(await MapUserAsync(user));
    }

    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [HttpPost("register")]
    public async Task<ActionResult<CurrentUserDto>> Register(RegisterRequest request)
    {
        var account = request.Account.Trim();
        var email = request.Email.Trim();
        if (await _userManager.FindByNameAsync(account) is not null)
            return Conflict(new ProblemDetails { Title = "註冊失敗", Detail = "此使用者帳號已被使用。", Status = StatusCodes.Status409Conflict });
        if (await _userManager.FindByEmailAsync(email) is not null)
            return Conflict(new ProblemDetails { Title = "註冊失敗", Detail = "此電子郵件已註冊。", Status = StatusCodes.Status409Conflict });

        var user = new ApplicationUser
        {
            UserName = account,
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(new ValidationProblemDetails(result.Errors.GroupBy(error => error.Code).ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray())));

        var roleResult = await _userManager.AddToRoleAsync(user, SystemRoles.Normal);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return BadRequest(new ProblemDetails { Title = "註冊失敗", Detail = "建立預設權限失敗。" });
        }
        await _signInManager.SignInAsync(user, isPersistent: false);
        var anonymousCart = _cartService.GetAnonymousCart(HttpContext);
        if (anonymousCart.Count > 0)
        {
            await _cartService.MergeAnonymousCartToUserAsync(user.Id, anonymousCart);
            _cartService.ClearAnonymousCart(HttpContext);
        }
        return Ok(await MapUserAsync(user));
    }

    [Authorize]
    [ValidateAntiForgeryToken]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return NoContent();
    }

    private async Task<CurrentUserDto> MapUserAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return new CurrentUserDto(true, user.Id, user.UserName, user.DisplayName, user.Email, roles.ToList());
    }
}