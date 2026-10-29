using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WillyShop.Contracts;
using WillyShop.Data;
using WillyShop.Logging;
using WillyShop.Models;
using WillyShop.Services;

namespace WillyShop.Controllers.Api.V1;

[ApiController]
[Authorize(Roles = SystemRoles.ProductManagers)]
[Route("api/v1/admin/products")]
public sealed class AdminProductsController : ApiControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly NotificationService _notifications;
    private readonly ProductQueryService _productQuery;
    private readonly ILogger<AdminProductsController> _logger;

    public AdminProductsController(
        ApplicationDbContext db,
        NotificationService notifications,
        ProductQueryService productQuery,
        ILogger<AdminProductsController> logger)
    {
        _db = db;
        _notifications = notifications;
        _productQuery = productQuery;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetAll(
        [FromQuery] AdminProductQuery query,
        CancellationToken cancellationToken)
    {
        if (query.ModifiedFrom > query.ModifiedTo)
            return ApiValidationFailure(new Dictionary<string, string[]>
            {
                [nameof(query.ModifiedTo)] = ["修改迄日不得早於修改起日。"]
            });
        return ApiSuccess(await _productQuery.SearchAdminAsync(query, cancellationToken));
    }
    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(SaveProductRequest request, CancellationToken cancellationToken)
    {
        var code = request.ProductCode.Trim();
        if (await _db.Products.AnyAsync(product => product.ProductCode == code, cancellationToken))
            return ApiFailure("商品代碼已存在。", StatusCodes.Status409Conflict);
        var product = new Product { ProductCode = code, CreatedAt = DateTime.UtcNow };
        Apply(product, request);
        _db.Products.Add(product);
        await _db.SaveChangesAsync(cancellationToken);
        _logger.ProductCreated(product.ProductCode, ActorUserId());
        return ApiSuccess(Map(product), "商品建立成功。", StatusCodes.Status201Created);
    }
    [HttpPut("{productCode}")]
    public async Task<ActionResult<ProductDto>> Update(
        string productCode,
        SaveProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await _db.Products.FindAsync([productCode], cancellationToken);
        if (product is null) return ApiFailure("找不到指定的商品。", StatusCodes.Status404NotFound);
        var wasOutOfStock = NotificationService.IsOutOfStock(product);
        Apply(product, request);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var notificationCount = 0;
        if (wasOutOfStock && NotificationService.IsPurchasable(product))
            notificationCount = await _notifications.NotifyBackInStockAsync(product, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        _logger.ProductUpdated(product.ProductCode, ActorUserId());
        if (notificationCount > 0)
            _logger.BackInStockNotificationsCreated(notificationCount, product.ProductCode);
        return ApiSuccess(Map(product), "商品更新成功。");
    }
    [HttpDelete("{productCode}")]
    public async Task<IActionResult> Delete(string productCode, CancellationToken cancellationToken)
    {
        var product = await _db.Products.FindAsync([productCode], cancellationToken);
        if (product is null) return ApiFailure("找不到指定的商品。", StatusCodes.Status404NotFound);
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var cartItems = await _db.UserCartItems.Where(item => item.ProductCode == productCode)
            .ToListAsync(cancellationToken);
        _db.UserCartItems.RemoveRange(cartItems);
        _db.Products.Remove(product);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        _logger.ProductDeleted(product.ProductCode, ActorUserId());
        return ApiSuccess<object?>(null, "商品刪除成功。");
    }

    private void Apply(Product product, SaveProductRequest request)
    {
        product.Name = request.Name.Trim();
        product.Category = request.Category?.Trim();
        product.Price = request.Price;
        product.IsOnSale = request.IsOnSale;
        product.SalePrice = request.IsOnSale ? request.SalePrice : null;
        product.StockQuantity = request.StockQuantity;
        product.Status = request.StockQuantity <= 0
            ? ProductStatus.數量不足
            : request.Status;
        product.ImagePath = request.ImagePath?.Trim();
        product.ModifiedBy = User.Identity?.Name;
        product.ModifiedAt = DateTime.UtcNow;
    }

    private static ProductDto Map(Product product) => ProductMappings.ToDto(product);

    private string? ActorUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);
}