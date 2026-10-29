using System.ComponentModel.DataAnnotations;
using WillyShop.Models;

namespace WillyShop.Contracts;

public sealed class SaveProductRequest : IValidatableObject
{
    [Required, StringLength(450)]
    public string ProductCode { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "999999999")]
    public decimal Price { get; set; }

    public bool IsOnSale { get; set; }

    [Range(typeof(decimal), "0.01", "999999999")]
    public decimal? SalePrice { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; set; }

    public ProductStatus Status { get; set; }

    [StringLength(500)]
    public string? ImagePath { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!IsOnSale)
            yield break;

        if (!SalePrice.HasValue)
        {
            yield return new ValidationResult("特價中商品必須填寫特價價格。", [nameof(SalePrice)]);
        }
        else if (SalePrice.Value >= Price)
        {
            yield return new ValidationResult("特價價格必須低於原價。", [nameof(SalePrice)]);
        }
    }
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

public sealed class AdminUserQuery
{
    [StringLength(100)]
    public string? Keyword { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

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