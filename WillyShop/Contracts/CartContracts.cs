using System.ComponentModel.DataAnnotations;

namespace WillyShop.Contracts;

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