using Microsoft.EntityFrameworkCore;
using WillyShop.Data;
using WillyShop.Models;

namespace WillyShop.IntegrationTests.Infrastructure;

internal static class TestData
{
    public static string NewSuffix() => Guid.NewGuid().ToString("N")[..12];

    public static Product CreateProduct(
        string suffix,
        int stockQuantity = 10,
        ProductStatus status = ProductStatus.可銷售,
        decimal price = 150m,
        bool isOnSale = false,
        decimal? salePrice = null)
        => new()
        {
            ProductCode = $"IT{suffix}",
            Name = $"整合測試商品 {suffix}",
            Price = price,
            IsOnSale = isOnSale,
            SalePrice = salePrice,
            Category = "自動測試",
            StockQuantity = stockQuantity,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };

    public static async Task AddProductAsync(WillyShopWebApplicationFactory factory, Product product)
    {
        await using var db = factory.CreateDbContext();
        db.Products.Add(product);
        await db.SaveChangesAsync();
    }

    public static async Task AddCartItemAsync(
        WillyShopWebApplicationFactory factory,
        string userId,
        string productCode,
        int quantity)
    {
        await using var db = factory.CreateDbContext();
        db.UserCartItems.Add(new UserCartItem
        {
            UserId = userId,
            ProductCode = productCode,
            Quantity = quantity
        });
        await db.SaveChangesAsync();
    }

    public static async Task<Order> AddOrderAsync(
        WillyShopWebApplicationFactory factory,
        string userId,
        string status = OrderStatuses.Pending)
    {
        await using var db = factory.CreateDbContext();
        var order = new Order
        {
            UserId = userId,
            TotalAmount = 100m,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    public static Task<int> CountOrdersAsync(ApplicationDbContext db, string userId)
        => db.Orders.AsNoTracking().CountAsync(order => order.UserId == userId);
}