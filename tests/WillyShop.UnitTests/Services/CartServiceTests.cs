using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using WillyShop.Data;
using WillyShop.Models;
using WillyShop.Services;
using WillyShop.UnitTests.Infrastructure;

namespace WillyShop.UnitTests.Services;

public sealed class CartServiceTests
{
    [Fact]
    public async Task AddToAnonymousCartAsync_相同商品重複加入_合併數量()
    {
        // Arrange
        var (service, context, db) = CreateService();
        await using var disposableDb = db;

        // Act
        await service.AddToAnonymousCartAsync(context, new CartItem { ProductCode = "P001", Quantity = 2 });
        await service.AddToAnonymousCartAsync(context, new CartItem { ProductCode = "P001", Quantity = 3 });

        // Assert
        var item = Assert.Single(service.GetAnonymousCart(context));
        Assert.Equal("P001", item.ProductCode);
        Assert.Equal(5, item.Quantity);
    }

    [Fact]
    public void GetAnonymousCart_Session內容損毀_回傳空購物車()
    {
        // Arrange
        var (service, context, db) = CreateService();
        using var disposableDb = db;
        context.Session.SetString("AnonCart", "not-json");

        // Act
        var cart = service.GetAnonymousCart(context);

        // Assert
        Assert.Empty(cart);
    }

    [Fact]
    public async Task ClearAnonymousCart_已有商品_移除Session購物車()
    {
        // Arrange
        var (service, context, db) = CreateService();
        await using var disposableDb = db;
        await service.AddToAnonymousCartAsync(context, new CartItem { ProductCode = "P001", Quantity = 1 });

        // Act
        service.ClearAnonymousCart(context);

        // Assert
        Assert.Empty(service.GetAnonymousCart(context));
    }

    private static (CartService Service, DefaultHttpContext Context, ApplicationDbContext Db) CreateService()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=WillyShopTests_UnitOnly;Trusted_Connection=True")
            .Options;
        var db = new ApplicationDbContext(options);
        var service = new CartService(db);
        var context = new DefaultHttpContext
        {
            Session = new TestSession()
        };
        return (service, context, db);
    }
}