using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WillyShop.Data;
using WillyShop.IntegrationTests.Infrastructure;
using WillyShop.Models;
using WillyShop.Services;

namespace WillyShop.IntegrationTests.Orders;

[Collection(IntegrationTestCollection.Name)]
public sealed class CheckoutReliabilityTests
{
    private readonly WillyShopWebApplicationFactory _factory;

    public CheckoutReliabilityTests(WillyShopWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task CheckoutAsync_第二次資料儲存失敗_訂單庫存與購物車全部回滾()
    {
        // Arrange
        var suffix = TestData.NewSuffix();
        var product = TestData.CreateProduct(suffix, stockQuantity: 2);
        await TestData.AddProductAsync(_factory, product);
        using var client = new ApiTestClient(_factory);
        var user = await client.RegisterAsync(suffix);
        await TestData.AddCartItemAsync(_factory, user.Id, product.ProductCode, 1);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(_factory.ConnectionString)
            .AddInterceptors(new FailOnSecondSaveChangesInterceptor())
            .Options;
        await using var faultingDb = new ApplicationDbContext(options);
        var service = new OrderApplicationService(
            faultingDb,
            new NotificationService(faultingDb),
            NullLogger<OrderApplicationService>.Instance);

        // Act
        var exception = await Record.ExceptionAsync(() => service.CheckoutAsync(
            user.Id,
            TestContext.Current.CancellationToken));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
        await using var verificationDb = _factory.CreateDbContext();
        Assert.Equal(0, await TestData.CountOrdersAsync(verificationDb, user.Id));
        Assert.False(await verificationDb.OrderItems.AnyAsync(
            item => item.ProductCode == product.ProductCode,
            TestContext.Current.CancellationToken));
        Assert.Equal(2,
            (await verificationDb.Products.AsNoTracking().SingleAsync(
                value => value.ProductCode == product.ProductCode,
                TestContext.Current.CancellationToken)).StockQuantity);
        Assert.True(await verificationDb.UserCartItems.AnyAsync(
            item => item.UserId == user.Id,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CheckoutAsync_兩位使用者同時購買最後一件_只建立一張訂單且庫存不為負數()
    {
        // Arrange
        var suffix = TestData.NewSuffix();
        var product = TestData.CreateProduct(suffix, stockQuantity: 1);
        await TestData.AddProductAsync(_factory, product);
        using var firstClient = new ApiTestClient(_factory);
        using var secondClient = new ApiTestClient(_factory);
        var firstUser = await firstClient.RegisterAsync($"a{suffix}");
        var secondUser = await secondClient.RegisterAsync($"b{suffix}");
        await TestData.AddCartItemAsync(_factory, firstUser.Id, product.ProductCode, 1);
        await TestData.AddCartItemAsync(_factory, secondUser.Id, product.ProductCode, 1);
        using var start = new Barrier(2);

        // Act
        var results = await Task.WhenAll(
            CheckoutConcurrentlyAsync(firstUser.Id, start),
            CheckoutConcurrentlyAsync(secondUser.Id, start));

        // Assert
        Assert.Equal(1, results.Count(result => result is null));
        Assert.Equal(1, results.Count(result => result is not null));
        await using var db = _factory.CreateDbContext();
        Assert.Equal(1, await db.Orders.CountAsync(
            order => order.UserId == firstUser.Id || order.UserId == secondUser.Id,
            TestContext.Current.CancellationToken));
        var storedProduct = await db.Products.AsNoTracking().SingleAsync(
            value => value.ProductCode == product.ProductCode,
            TestContext.Current.CancellationToken);
        Assert.Equal(0, storedProduct.StockQuantity);
        Assert.Equal(ProductStatus.數量不足, storedProduct.Status);
    }

    private async Task<Exception?> CheckoutConcurrentlyAsync(string userId, Barrier start)
    {
        await using var db = _factory.CreateDbContext();
        var service = new OrderApplicationService(
            db,
            new NotificationService(db),
            NullLogger<OrderApplicationService>.Instance);
        start.SignalAndWait(TimeSpan.FromSeconds(10));
        return await Record.ExceptionAsync(() => service.CheckoutAsync(
            userId,
            TestContext.Current.CancellationToken));
    }
}