using System.ComponentModel.DataAnnotations;
using WillyShop.Models;

namespace WillyShop.Contracts;

public sealed record ProductDto(
    string ProductCode,
    string Name,
    decimal Price,
    bool IsOnSale,
    decimal? SalePrice,
    decimal EffectivePrice,
    string PriceStatusName,
    string? Category,
    int StockQuantity,
    ProductStatus Status,
    string StatusName,
    string? ImageUrl,
    DateTime CreatedAt,
    string? ModifiedBy,
    DateTime? ModifiedAt);

public class ProductQuery
{
    [StringLength(450)]
    public string? ProductCode { get; set; }

    [StringLength(200)]
    public string? ProductName { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }

    public DateTime? ModifiedFrom { get; set; }
    public DateTime? ModifiedTo { get; set; }

    [RegularExpression("^(productCode|productName|price|modifiedAt)$", ErrorMessage = "不支援的排序欄位。")]
    public string SortBy { get; set; } = "productCode";

    [RegularExpression("^(asc|desc)$", ErrorMessage = "排序方向必須為 asc 或 desc。")]
    public string SortDirection { get; set; } = "asc";

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}

public sealed class AdminProductQuery : ProductQuery
{
    public ProductStatus? Status { get; set; }
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public static class ProductMappings
{
    public static ProductDto ToDto(Product product)
    {
        var status = product.StockQuantity <= 0
            ? ProductStatus.數量不足
            : product.Status;

        return new ProductDto(
            product.ProductCode,
            product.Name,
            product.Price,
            product.IsOnSale,
            product.SalePrice,
            product.EffectivePrice,
            product.IsOnSale ? "特價" : "原價",
            product.Category,
            product.StockQuantity,
            status,
            status.ToString(),
            Services.CartApplicationService.NormalizeImageUrl(product.ImagePath),
            product.CreatedAt,
            product.ModifiedBy,
            product.ModifiedAt);
    }
}