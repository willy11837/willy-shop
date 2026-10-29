using System.ComponentModel.DataAnnotations;
using WillyShop.Models;

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
    string StatusName,
    DateTime? ModifiedAt,
    DateTime? ShippedAt,
    DateTime? CompletedAt,
    DateTime? CancelledAt,
    DateTime? ReturnRequestedAt,
    string? ReturnReason,
    DateTime? ReturnReviewedAt,
    string? ReturnReviewNote,
    DateTime? RefundCompletedAt,
    decimal? RefundAmount,
    string? RefundNote,
    IReadOnlyList<OrderItemDto> Items);

public sealed record AdminOrderDto(
    int Id,
    string UserId,
    string Account,
    string? DisplayName,
    string? Email,
    decimal TotalAmount,
    DateTime CreatedAt,
    string Status,
    string StatusName,
    DateTime? ModifiedAt,
    DateTime? ShippedAt,
    DateTime? CompletedAt,
    DateTime? CancelledAt,
    DateTime? ReturnRequestedAt,
    string? ReturnReason,
    DateTime? ReturnReviewedAt,
    string? ReturnReviewedBy,
    string? ReturnReviewNote,
    DateTime? RefundCompletedAt,
    string? RefundProcessedBy,
    decimal? RefundAmount,
    string? RefundNote,
    IReadOnlyList<OrderItemDto> Items);

public sealed class AdminOrderQuery
{
    [StringLength(100)]
    public string? Keyword { get; set; }

    [StringLength(30)]
    public string? Status { get; set; }

    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedTo { get; set; }

    [RegularExpression("^(createdAt|totalAmount)$")]
    public string SortBy { get; set; } = "createdAt";

    [RegularExpression("^(asc|desc)$")]
    public string SortDirection { get; set; } = "desc";

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

public sealed class ReturnRequest
{
    [Required, StringLength(500, MinimumLength = 5)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class ReviewReturnRequest
{
    [StringLength(500)]
    public string? Note { get; set; }
}

public sealed class CompleteRefundRequest
{
    [StringLength(500)]
    public string? Note { get; set; }
}