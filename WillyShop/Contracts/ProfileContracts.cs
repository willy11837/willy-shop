using System.ComponentModel.DataAnnotations;

namespace WillyShop.Contracts;

public sealed record ProfileDto(
    string Id,
    string Account,
    string DisplayName,
    string Email,
    IReadOnlyList<string> Roles,
    DateTime CreatedAt,
    DateTime? ModifiedAt);

public sealed class UpdateProfileRequest
{
    [Required, StringLength(50)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    [Required(ErrorMessage = "請輸入目前密碼。")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "請輸入新密碼。")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "新密碼長度必須介於 8 到 100 個字元。")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "請再次輸入新密碼。")]
    [Compare(nameof(NewPassword), ErrorMessage = "新密碼與確認密碼不相符。")]
    public string ConfirmPassword { get; set; } = string.Empty;
}