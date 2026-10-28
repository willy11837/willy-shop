using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace WillyShop.Contracts;

public sealed record CurrentUserDto(
    string Id,
    string Account,
    string? DisplayName,
    string? Email,
    IReadOnlyList<string> Roles);

public sealed record AuthResponse(
    string AccessToken,
    DateTime ExpiresAt,
    CurrentUserDto User,
    [property: JsonIgnore] string RefreshToken,
    [property: JsonIgnore] DateTime RefreshTokenExpiresAt);

public sealed class LoginRequest
{
    [Required(ErrorMessage = "請輸入帳號。")]
    public string Account { get; set; } = string.Empty;

    [Required(ErrorMessage = "請輸入密碼。")]
    public string Password { get; set; } = string.Empty;
}

public sealed class RegisterRequest
{
    [Required, StringLength(30, MinimumLength = 3)]
    [RegularExpression("^[A-Za-z0-9._-]+$", ErrorMessage = "帳號只能包含英文字母、數字、句點、底線或連字號。")]
    public string Account { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [Required, Compare(nameof(Password), ErrorMessage = "密碼與確認密碼不相符。")]
    public string ConfirmPassword { get; set; } = string.Empty;
}