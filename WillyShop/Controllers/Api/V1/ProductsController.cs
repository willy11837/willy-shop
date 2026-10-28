using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WillyShop.Contracts;
using WillyShop.Models;
using WillyShop.Services;

namespace WillyShop.Controllers.Api.V1;

[ApiController]
[Route("api/v1/products")]
public sealed class ProductsController : ApiControllerBase
{
    private readonly ProductQueryService _products;
    private readonly WillyShop.Data.ApplicationDbContext _db;

    public ProductsController(ProductQueryService products, WillyShop.Data.ApplicationDbContext db)
    {
        _products = products;
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetAll(
        [FromQuery] ProductQuery query,
        CancellationToken cancellationToken)
    {
        if (query.ModifiedFrom > query.ModifiedTo)
            ModelState.AddModelError(nameof(query.ModifiedTo), "修改迄日不得早於修改起日。");
        if (!ModelState.IsValid)
            return ApiValidationFailure(ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray()));

        return ApiSuccess(await _products.SearchAsync(query, cancellationToken));
    }

    [HttpGet("{productCode}")]
    public async Task<ActionResult<ProductDto>> GetByCode(
        string productCode,
        CancellationToken cancellationToken)
    {
        var product = await _db.Products.AsNoTracking()
            .FirstOrDefaultAsync(value => value.ProductCode == productCode, cancellationToken);
        return product is null
            ? ApiFailure("找不到指定的商品。", StatusCodes.Status404NotFound)
            : ApiSuccess(ProductMappings.ToDto(product));
    }

    [HttpGet("on-sale")]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> GetOnSale(CancellationToken cancellationToken)
    {
        var products = await _db.Products.AsNoTracking()
            .Where(product => product.Status == ProductStatus.可銷售
                && product.StockQuantity > 0
                && product.IsOnSale
                && product.SalePrice.HasValue)
            .OrderBy(product => product.ProductCode)
            .Take(100)
            .ToListAsync(cancellationToken);

        return ApiSuccess<IReadOnlyList<ProductDto>>(products.Select(ProductMappings.ToDto).ToList());
    }
}