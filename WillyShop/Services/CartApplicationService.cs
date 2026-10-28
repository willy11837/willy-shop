using Microsoft.EntityFrameworkCore;
using WillyShop.Contracts;
using WillyShop.Data;
using WillyShop.Models;

namespace WillyShop.Services;

public sealed class CartApplicationService
{
    private readonly ApplicationDbContext _db;
    private readonly CartService _cartService;

    public CartApplicationService(ApplicationDbContext db, CartService cartService)
    {
        _db = db;
        _cartService = cartService;
    }

    public async Task<CartDto> GetAsync(HttpContext httpContext, string? userId)
    {
        List<(int? Id, string ProductCode, int Quantity)> rawItems;
        if (string.IsNullOrWhiteSpace(userId))
        {
            rawItems = _cartService.GetAnonymousCart(httpContext)
                .Select(item => ((int?)null, item.ProductCode, item.Quantity))
                .ToList();
        }
        else
        {
            rawItems = await _db.UserCartItems
                .Where(item => item.UserId == userId)
                .Select(item => new ValueTuple<int?, string, int>(item.Id, item.ProductCode, item.Quantity))
                .ToListAsync();
        }

        var codes = rawItems.Select(item => item.ProductCode).Distinct().ToList();
        var products = await _db.Products
            .Where(product => codes.Contains(product.ProductCode))
            .ToDictionaryAsync(product => product.ProductCode);

        var items = rawItems.Select(item =>
        {
            products.TryGetValue(item.ProductCode, out var product);
            var price = product?.EffectivePrice ?? 0m;
            return new CartItemDto(
                item.Id,
                item.ProductCode,
                product?.Name ?? item.ProductCode,
                price,
                item.Quantity,
                price * item.Quantity,
                product?.StockQuantity ?? 0,
                NormalizeImageUrl(product?.ImagePath));
        }).ToList();

        return new CartDto(items, items.Sum(item => item.Quantity), items.Sum(item => item.Subtotal));
    }

    public async Task AddAsync(HttpContext httpContext, string? userId, string productCode, int quantity)
    {
        var product = await _db.Products.FindAsync(productCode);
        if (product is null)
            throw new KeyNotFoundException("找不到指定商品。");
        if (product.Status != ProductStatus.可銷售 || product.StockQuantity <= 0)
            throw new InvalidOperationException("此商品目前無法購買。");

        quantity = Math.Clamp(quantity, 1, 999);
        if (string.IsNullOrWhiteSpace(userId))
        {
            var cart = _cartService.GetAnonymousCart(httpContext);
            var existing = cart.FirstOrDefault(item => item.ProductCode == productCode);
            var nextQuantity = (existing?.Quantity ?? 0) + quantity;
            if (nextQuantity > product.StockQuantity)
                throw new InvalidOperationException("加入數量超過目前庫存。");
            await _cartService.AddToAnonymousCartAsync(httpContext, new CartItem { ProductCode = productCode, Quantity = quantity });
            return;
        }

        var userItem = await _db.UserCartItems.FirstOrDefaultAsync(item => item.UserId == userId && item.ProductCode == productCode);
        var userNextQuantity = (userItem?.Quantity ?? 0) + quantity;
        if (userNextQuantity > product.StockQuantity)
            throw new InvalidOperationException("加入數量超過目前庫存。");
        if (userItem is null)
            _db.UserCartItems.Add(new UserCartItem { UserId = userId, ProductCode = productCode, Quantity = quantity });
        else
            userItem.Quantity = userNextQuantity;
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(HttpContext httpContext, string? userId, string productCode, int quantity)
    {
        var product = await _db.Products.FindAsync(productCode) ?? throw new KeyNotFoundException("找不到指定商品。");
        if (quantity < 1 || quantity > product.StockQuantity)
            throw new InvalidOperationException("購買數量必須大於零且不可超過庫存。");

        if (string.IsNullOrWhiteSpace(userId))
        {
            var items = _cartService.GetAnonymousCart(httpContext);
            var item = items.FirstOrDefault(value => value.ProductCode == productCode) ?? throw new KeyNotFoundException("購物車中沒有此商品。");
            item.Quantity = quantity;
            httpContext.Session.SetString("AnonCart", System.Text.Json.JsonSerializer.Serialize(items));
            return;
        }

        var userItem = await _db.UserCartItems.FirstOrDefaultAsync(item => item.UserId == userId && item.ProductCode == productCode)
            ?? throw new KeyNotFoundException("購物車中沒有此商品。");
        userItem.Quantity = quantity;
        await _db.SaveChangesAsync();
    }

    public async Task RemoveAsync(HttpContext httpContext, string? userId, string productCode)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            var items = _cartService.GetAnonymousCart(httpContext);
            items.RemoveAll(item => item.ProductCode == productCode);
            httpContext.Session.SetString("AnonCart", System.Text.Json.JsonSerializer.Serialize(items));
            return;
        }

        var userItem = await _db.UserCartItems.FirstOrDefaultAsync(item => item.UserId == userId && item.ProductCode == productCode);
        if (userItem is null) return;
        _db.UserCartItems.Remove(userItem);
        await _db.SaveChangesAsync();
    }

    public static string? NormalizeImageUrl(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        return path.StartsWith("~/", StringComparison.Ordinal) ? path[1..] : path;
    }
}