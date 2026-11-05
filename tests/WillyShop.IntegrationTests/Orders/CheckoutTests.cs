using System.Net;
using Microsoft.EntityFrameworkCore;
using WillyShop.Contracts;
using WillyShop.IntegrationTests.Infrastructure;
using WillyShop.Models;

namespace WillyShop.IntegrationTests.Orders;

[Collection(IntegrationTestCollection.Name)]
public sealed class CheckoutTests
{
    private readonly WillyShopWebApplicationFactory _factory;

    public CheckoutTests(WillyShopWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task CheckoutAsync_空購物車_回傳400且不建立訂單()
    {
        // Arrange
        var suffix = TestData.NewSuffix();
        using var client = new ApiTestClient(_factory);
        var user = await client.RegisterAsync(suffix);

        // Act
        var response = await client.PostAsync("/api/v1/orders/checkout");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = _factory.CreateDbContext();
        Assert.Equal(0, await TestData.CountOrdersAsync(db, user.Id));
    }

    [Fact]
    public async Task CheckoutAsync_商品已不存在_不建立訂單且保留購物車()
    {
        // Arrange
        var suffix = TestData.NewSuffix();
        using var client = new ApiTestClient(_factory);
        var user = await client.RegisterAsync(suffix);
        await TestData.AddCartItemAsync(_factory, user.Id, $"MISSING-{suffix}", 1);

        // Act
        var response = await client.PostAsync("/api/v1/orders/checkout");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = _factory.CreateDbContext();
        Assert.Equal(0, await TestData.CountOrdersAsync(db, user.Id));
        Assert.True(await db.UserCartItems.AnyAsync(
            item => item.UserId == user.Id,
            TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(ProductStatus.凍結, 2, 1)]
    [InlineData(ProductStatus.可銷售, 1, 2)]
    public async Task CheckoutAsync_商品不可銷售或庫存不足_不建立訂單且不扣庫存(
        ProductStatus status,
        int stockQuantity,
        int cartQuantity)
    {
        // Arrange
        var suffix = TestData.NewSuffix();
        var product = TestData.CreateProduct(suffix, stockQuantity, status);
        await TestData.AddProductAsync(_factory, product);
        using var client = new ApiTestClient(_factory);
        var user = await client.RegisterAsync(suffix);
        await TestData.AddCartItemAsync(_factory, user.Id, product.ProductCode, cartQuantity);

        // Act
        var response = await client.PostAsync("/api/v1/orders/checkout");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = _factory.CreateDbContext();
        Assert.Equal(0, await TestData.CountOrdersAsync(db, user.Id));
        Assert.Equal(stockQuantity,
            (await db.Products.AsNoTracking().SingleAsync(
                value => value.ProductCode == product.ProductCode,
                TestContext.Current.CancellationToken)).StockQuantity);
        Assert.True(await db.UserCartItems.AnyAsync(
            item => item.UserId == user.Id,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CheckoutAsync_特價且庫存剛好足夠_建立價格快照扣完庫存並清空購物車()
    {
        // Arrange
        var suffix = TestData.NewSuffix();
        var product = TestData.CreateProduct(
            suffix,
            stockQuantity: 2,
            price: 150m,
            isOnSale: true,
            salePrice: 120.25m);
        await TestData.AddProductAsync(_factory, product);
        using var client = new ApiTestClient(_factory);
        var user = await client.RegisterAsync(suffix);
        await TestData.AddCartItemAsync(_factory, user.Id, product.ProductCode, 2);

        // Act
        var response = await client.PostAsync("/api/v1/orders/checkout");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await ApiTestClient.ReadEnvelopeAsync<OrderDto>(response);
        Assert.NotNull(envelope.Data);
        Assert.Equal(240.50m, envelope.Data.TotalAmount);
        Assert.Equal(OrderStatuses.Pending, envelope.Data.Status);
        var item = Assert.Single(envelope.Data.Items);
        Assert.Equal(120.25m, item.UnitPrice);
        Assert.Equal(240.50m, item.Subtotal);

        await using var db = _factory.CreateDbContext();
        var storedProduct = await db.Products.AsNoTracking().SingleAsync(
            value => value.ProductCode == product.ProductCode,
            TestContext.Current.CancellationToken);
        Assert.Equal(0, storedProduct.StockQuantity);
        Assert.Equal(ProductStatus.數量不足, storedProduct.Status);
        Assert.False(await db.UserCartItems.AnyAsync(
            value => value.UserId == user.Id,
            TestContext.Current.CancellationToken));
        Assert.Equal(1, await TestData.CountOrdersAsync(db, user.Id));
    }
}