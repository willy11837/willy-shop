using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WillyShop.Contracts;
using WillyShop.Data;
using WillyShop.Models;
using WillyShop.Services;

namespace WillyShop.Controllers.Api.V1;

[ApiController]
[Authorize(Roles = SystemRoles.ProductManagers)]
[Route("api/v1/admin/products")]
public sealed class AdminProductsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public AdminProductsController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> GetAll()
    {
        var products = await _db.Products.AsNoTracking().OrderBy(product => product.ProductCode).ToListAsync();
        return Ok(products.Select(Map).ToList());
    }

    [ValidateAntiForgeryToken]
    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create(SaveProductRequest request)
    {
        var code = request.ProductCode.Trim();
        if (await _db.Products.AnyAsync(product => product.ProductCode == code))
            return Conflict(Problem("商品代碼已存在。", statusCode: 409));
        var product = new Product { ProductCode = code, CreatedAt = DateTime.UtcNow };
        Apply(product, request);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAll), new { code }, Map(product));
    }

    [ValidateAntiForgeryToken]
    [HttpPut("{productCode}")]
    public async Task<ActionResult<ProductDto>> Update(string productCode, SaveProductRequest request)
    {
        var product = await _db.Products.FindAsync(productCode);
        if (product is null) return NotFound();
        Apply(product, request);
        await _db.SaveChangesAsync();
        return Ok(Map(product));
    }

    [ValidateAntiForgeryToken]
    [HttpDelete("{productCode}")]
    public async Task<IActionResult> Delete(string productCode)
    {
        var product = await _db.Products.FindAsync(productCode);
        if (product is null) return NotFound();
        await using var transaction = await _db.Database.BeginTransactionAsync();
        var cartItems = await _db.UserCartItems.Where(item => item.ProductCode == productCode).ToListAsync();
        _db.UserCartItems.RemoveRange(cartItems);
        _db.Products.Remove(product);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return NoContent();
    }

    private void Apply(Product product, SaveProductRequest request)
    {
        product.Name = request.Name.Trim();
        product.Category = request.Category?.Trim();
        product.Price = request.Price;
        product.StockQuantity = request.StockQuantity;
        product.Status = request.Status;
        product.ImagePath = request.ImagePath?.Trim();
        product.ModifiedBy = User.Identity?.Name;
        product.ModifiedAt = DateTime.UtcNow;
    }

    private static ProductDto Map(Product product) => new(product.ProductCode, product.Name, product.Price, product.Category,
        product.StockQuantity, product.Status, product.Status.ToString(), CartApplicationService.NormalizeImageUrl(product.ImagePath),
        product.CreatedAt, product.ModifiedBy, product.ModifiedAt);
}