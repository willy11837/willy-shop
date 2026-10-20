using System.ComponentModel.DataAnnotations;
using WillyShop.Models;

namespace WillyShop.Contracts;

public sealed record ProductDto(
    string ProductCode,
    string Name,
    decimal Price,
    string? Category,
    int StockQuantity,
    ProductStatus Status,
    string StatusName,
    string? ImageUrl,
    DateTime CreatedAt,
    string? ModifiedBy,
    DateTime? ModifiedAt);

public sealed record CurrentUserDto(
    bool IsAuthenticated,
    string? Id,
    string? Account,
    string? DisplayName,
    string? Email,
    IReadOnlyList<string> Roles);

public sealed class LoginRequest
{
    [Required(ErrorMessage = "請輸入帳號。")]
    public string Account { get; set; } = string.Empty;

    [Required(ErrorMessage = "請輸入密碼。")]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
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

public sealed class CartQuantityRequest
{
    [Range(1, 999, ErrorMessage = "數量必須介於 1 到 999。")]
    public int Quantity { get; set; } = 1;
}

public sealed record CartItemDto(
    int? Id,
    string ProductCode,
    string Name,
    decimal UnitPrice,
    int Quantity,
    decimal Subtotal,
    int StockQuantity,
    string? ImageUrl);

public sealed record CartDto(IReadOnlyList<CartItemDto> Items, int TotalQuantity, decimal TotalAmount);

public sealed record OrderItemDto(
    string ProductCode,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal Subtotal);

public sealed record OrderDto(
    int Id,
    decimal TotalAmount,
    DateTime CreatedAt,
    string Status,
    IReadOnlyList<OrderItemDto> Items);

public sealed class SaveProductRequest
{
    [Required, StringLength(450)]
    public string ProductCode { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "999999999")]
    public decimal Price { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; set; }

    public ProductStatus Status { get; set; }

    [StringLength(500)]
    public string? ImagePath { get; set; }
}

public sealed record AdminUserDto(
    string Id,
    string Account,
    string? DisplayName,
    string? Email,
    string Role,
    DateTime CreatedAt,
    string? ModifiedBy,
    DateTime? ModifiedAt);

public sealed class CreateUserRequest
{
    [Required, StringLength(30, MinimumLength = 3)]
    [RegularExpression("^[A-Za-z0-9._-]+$")]
    public string Account { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = SystemRoles.Normal;
}

public sealed class UpdateUserRequest
{
    [Required, StringLength(50)]
    public string DisplayName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string? NewPassword { get; set; }

    [Required]
    public string Role { get; set; } = SystemRoles.Normal;
}