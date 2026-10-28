using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Contracts;
using WebApplication1.Data;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers.Api.V1;

[ApiController]
[Route("api/v1/products")]
public sealed class ProductsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ProductsController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> GetAll([FromQuery] string? search, [FromQuery] string? category)
    {
        var query = _db.Products.AsNoTracking().Where(product => product.Status == ProductStatus.可銷售 && product.StockQuantity > 0);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(product => product.ProductCode.Contains(value) || product.Name.Contains(value));
        }
        if (!string.IsNullOrWhiteSpace(category))
        {
            var value = category.Trim();
            query = query.Where(product => product.Category == value);
        }

        var products = await query.OrderBy(product => product.ProductCode).Take(100).ToListAsync();
        return Ok(products.Select(Map).ToList());
    }

    [HttpGet("{productCode}")]
    public async Task<ActionResult<ProductDto>> GetByCode(string productCode)
    {
        var product = await _db.Products.AsNoTracking().FirstOrDefaultAsync(value => value.ProductCode == productCode);
        return product is null ? NotFound() : Ok(Map(product));
    }

    private static ProductDto Map(Product product) => new(
        product.ProductCode,
        product.Name,
        product.Price,
        product.Category,
        product.StockQuantity,
        product.Status,
        product.Status.ToString(),
        CartApplicationService.NormalizeImageUrl(product.ImagePath),
        product.CreatedAt,
        product.ModifiedBy,
        product.ModifiedAt);
}