using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WillyShop.Contracts;
using WillyShop.Models;
using WillyShop.Services;
using WillyShop.Services.Auth;

namespace WillyShop.Controllers.Api.V1;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ApiControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly CartService _cartService;
    private readonly ITokenService _tokenService;
    private readonly IAntiforgery _antiforgery;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        CartService cartService,
        ITokenService tokenService,
        IAntiforgery antiforgery)
    {
        _userManager = userManager;
        _cartService = cartService;
        _tokenService = tokenService;
        _antiforgery = antiforgery;
    }

    [AllowAnonymous]
    [HttpGet("csrf-token")]
    public ActionResult<object> GetCsrfToken()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        return ApiSuccess(new { token = tokens.RequestToken });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserDto>> Me()
    {
        var user = await _userManager.GetUserAsync(User);
        return user is null
            ? ApiFailure("需要有效的登入憑證。", StatusCodes.Status401Unauthorized)
            : ApiSuccess(await MapUserAsync(user));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var account = request.Account.Trim();
        var user = await _userManager.FindByNameAsync(account);
        if (user is null || await _userManager.IsLockedOutAsync(user))
            return LoginFailed();

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            await _userManager.AccessFailedAsync(user);
            return LoginFailed();
        }

        await _userManager.ResetAccessFailedCountAsync(user);
        await MergeAnonymousCartAsync(user, cancellationToken);
        var response = await _tokenService.IssueAsync(user, GetIpAddress(), cancellationToken);
        _tokenService.WriteRefreshTokenCookie(Response, response.RefreshToken, response.RefreshTokenExpiresAt);
        return ApiSuccess(response, "登入成功。");
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var account = request.Account.Trim();
        var email = request.Email.Trim();
        if (await _userManager.FindByNameAsync(account) is not null)
            return ApiFailure("此使用者帳號已被使用。", StatusCodes.Status409Conflict);
        if (await _userManager.FindByEmailAsync(email) is not null)
            return ApiFailure("此電子郵件已註冊。", StatusCodes.Status409Conflict);

        var user = new ApplicationUser
        {
            UserName = account,
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded) return IdentityErrors(result);

        var roleResult = await _userManager.AddToRoleAsync(user, SystemRoles.Normal);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return IdentityErrors(roleResult);
        }

        await MergeAnonymousCartAsync(user, cancellationToken);
        var response = await _tokenService.IssueAsync(user, GetIpAddress(), cancellationToken);
        _tokenService.WriteRefreshTokenCookie(Response, response.RefreshToken, response.RefreshTokenExpiresAt);
        return ApiSuccess(response, "註冊成功。", StatusCodes.Status201Created);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue(TokenService.RefreshTokenCookieName, out var refreshToken)
            || string.IsNullOrWhiteSpace(refreshToken))
            return ApiFailure("Refresh Token 無效或已過期。", StatusCodes.Status401Unauthorized);

        var response = await _tokenService.RotateAsync(refreshToken, GetIpAddress(), cancellationToken);
        if (response is null)
        {
            _tokenService.DeleteRefreshTokenCookie(Response);
            return ApiFailure("Refresh Token 無效或已過期。", StatusCodes.Status401Unauthorized);
        }

        _tokenService.WriteRefreshTokenCookie(Response, response.RefreshToken, response.RefreshTokenExpiresAt);
        return ApiSuccess(response, "存取權杖更新成功。");
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (Request.Cookies.TryGetValue(TokenService.RefreshTokenCookieName, out var refreshToken)
            && !string.IsNullOrWhiteSpace(refreshToken))
            await _tokenService.RevokeAsync(refreshToken, GetIpAddress(), "使用者登出。", cancellationToken);

        _tokenService.DeleteRefreshTokenCookie(Response);
        return ApiSuccess<object?>(null, "登出成功。");
    }

    private async Task MergeAnonymousCartAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var anonymousCart = _cartService.GetAnonymousCart(HttpContext);
        if (anonymousCart.Count == 0) return;

        await _cartService.MergeAnonymousCartToUserAsync(user.Id, anonymousCart, cancellationToken);
        _cartService.ClearAnonymousCart(HttpContext);
    }

    private async Task<CurrentUserDto> MapUserAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return new CurrentUserDto(user.Id, user.UserName ?? string.Empty, user.DisplayName, user.Email, roles.ToList());
    }

    private ActionResult LoginFailed() => ApiFailure("登入失敗，帳號或密碼錯誤。", StatusCodes.Status401Unauthorized);

    private ObjectResult IdentityErrors(IdentityResult result) => ApiValidationFailure(
        result.Errors.GroupBy(error => error.Code).ToDictionary(
            group => group.Key,
            group => group.Select(error => error.Description).ToArray()));

    private string? GetIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString();
}