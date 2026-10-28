using Microsoft.EntityFrameworkCore;
using WillyShop.Contracts;
using WillyShop.Data;
using WillyShop.Models;

namespace WillyShop.Services;

public sealed class ProductQueryService
{
    private readonly ApplicationDbContext _db;

    public ProductQueryService(ApplicationDbContext db) => _db = db;

    public async Task<PagedResult<ProductDto>> SearchAsync(ProductQuery request, CancellationToken cancellationToken)
    {
        var query = _db.Products.AsNoTracking()
            .Where(product => product.Status == ProductStatus.可銷售 && product.StockQuantity > 0);

        if (!string.IsNullOrWhiteSpace(request.ProductCode))
        {
            var value = request.ProductCode.Trim();
            query = query.Where(product => product.ProductCode.Contains(value));
        }
        if (!string.IsNullOrWhiteSpace(request.ProductName))
        {
            var value = request.ProductName.Trim();
            query = query.Where(product => product.Name.Contains(value));
        }
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var value = request.Category.Trim();
            query = query.Where(product => product.Category == value);
        }
        if (request.ModifiedFrom.HasValue)
            query = query.Where(product => product.ModifiedAt >= request.ModifiedFrom.Value);
        if (request.ModifiedTo.HasValue)
            query = query.Where(product => product.ModifiedAt <= request.ModifiedTo.Value);

        query = (request.SortBy, request.SortDirection) switch
        {
            ("productCode", "desc") => query.OrderByDescending(product => product.ProductCode),
            ("productName", "asc") => query.OrderBy(product => product.Name).ThenBy(product => product.ProductCode),
            ("productName", "desc") => query.OrderByDescending(product => product.Name).ThenBy(product => product.ProductCode),
            ("price", "asc") => query.OrderBy(product => product.IsOnSale && product.SalePrice.HasValue ? product.SalePrice.Value : product.Price)
                .ThenBy(product => product.ProductCode),
            ("price", "desc") => query.OrderByDescending(product => product.IsOnSale && product.SalePrice.HasValue ? product.SalePrice.Value : product.Price)
                .ThenBy(product => product.ProductCode),
            ("modifiedAt", "asc") => query.OrderBy(product => product.ModifiedAt).ThenBy(product => product.ProductCode),
            ("modifiedAt", "desc") => query.OrderByDescending(product => product.ModifiedAt).ThenBy(product => product.ProductCode),
            _ => query.OrderBy(product => product.ProductCode)
        };

        var totalCount = await query.CountAsync(cancellationToken);
        var products = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<ProductDto>(products.Select(ProductMappings.ToDto).ToList(), request.Page, request.PageSize, totalCount);
    }
}