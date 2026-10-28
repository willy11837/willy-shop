namespace WillyShop.Contracts;

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