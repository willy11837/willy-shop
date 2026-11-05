using System.Net;
using Microsoft.EntityFrameworkCore;
using WillyShop.Contracts;
using WillyShop.IntegrationTests.Infrastructure;
using WillyShop.Models;

namespace WillyShop.IntegrationTests.Cart;

[Collection(IntegrationTestCollection.Name)]
public sealed class CartApiTests
{
    private readonly WillyShopWebApplicationFactory _factory;

    public CartApiTests(WillyShopWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1000)]
    public async Task Add_數量超出一到999_回傳400且不建立購物車項目(int quantity)
    {
        // Arrange
        var suffix = TestData.NewSuffix();
        var product = TestData.CreateProduct(suffix, stockQuantity: 1000);
        await TestData.AddProductAsync(_factory, product);
        using var client = new ApiTestClient(_factory);
        var user = await client.RegisterAsync(suffix);

        // Act
        var response = await client.PostAsync($"/api/v1/cart/items/{product.ProductCode}", new { quantity });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = _factory.CreateDbContext();
        Assert.False(await db.UserCartItems.AnyAsync(
            item => item.UserId == user.Id,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Add_商品不存在_回傳404且不建立購物車項目()
    {
        // Arrange
        var suffix = TestData.NewSuffix();
        using var client = new ApiTestClient(_factory);
        var user = await client.RegisterAsync(suffix);

        // Act
        var response = await client.PostAsync("/api/v1/cart/items/NOT-FOUND", new { quantity = 1 });

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await using var db = _factory.CreateDbContext();
        Assert.False(await db.UserCartItems.AnyAsync(
            item => item.UserId == user.Id,
            TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(ProductStatus.凍結, 10)]
    [InlineData(ProductStatus.數量不足, 0)]
    public async Task Add_商品不可銷售_回傳400且不建立購物車項目(ProductStatus status, int stockQuantity)
    {
        // Arrange
        var suffix = TestData.NewSuffix();
        var product = TestData.CreateProduct(suffix, stockQuantity, status);
        await TestData.AddProductAsync(_factory, product);
        using var client = new ApiTestClient(_factory);
        var user = await client.RegisterAsync(suffix);

        // Act
        var response = await client.PostAsync($"/api/v1/cart/items/{product.ProductCode}", new { quantity = 1 });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = _factory.CreateDbContext();
        Assert.False(await db.UserCartItems.AnyAsync(
            item => item.UserId == user.Id,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Add_數量剛好等於庫存_成功建立購物車項目()
    {
        // Arrange
        var suffix = TestData.NewSuffix();
        var product = TestData.CreateProduct(suffix, stockQuantity: 3);
        await TestData.AddProductAsync(_factory, product);
        using var client = new ApiTestClient(_factory);
        var user = await client.RegisterAsync(suffix);

        // Act
        var response = await client.PostAsync($"/api/v1/cart/items/{product.ProductCode}", new { quantity = 3 });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await ApiTestClient.ReadEnvelopeAsync<CartDto>(response);
        Assert.Equal(3, envelope.Data?.TotalQuantity);
        await using var db = _factory.CreateDbContext();
        var item = await db.UserCartItems.AsNoTracking().SingleAsync(
            value => value.UserId == user.Id,
            TestContext.Current.CancellationToken);
        Assert.Equal(3, item.Quantity);
    }

    [Fact]
    public async Task Add_累計數量超過庫存_回傳400且保留原數量()
    {
        // Arrange
        var suffix = TestData.NewSuffix();
        var product = TestData.CreateProduct(suffix, stockQuantity: 3);
        await TestData.AddProductAsync(_factory, product);
        using var client = new ApiTestClient(_factory);
        var user = await client.RegisterAsync(suffix);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsync($"/api/v1/cart/items/{product.ProductCode}", new { quantity = 2 })).StatusCode);

        // Act
        var response = await client.PostAsync($"/api/v1/cart/items/{product.ProductCode}", new { quantity = 2 });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = _factory.CreateDbContext();
        var item = await db.UserCartItems.AsNoTracking().SingleAsync(
            value => value.UserId == user.Id,
            TestContext.Current.CancellationToken);
        Assert.Equal(2, item.Quantity);
    }

    [Fact]
    public async Task Get_兩位使用者加入相同商品_各自只能看到自己的數量()
    {
        // Arrange
        var suffix = TestData.NewSuffix();
        var product = TestData.CreateProduct(suffix, stockQuantity: 10);
        await TestData.AddProductAsync(_factory, product);
        using var firstClient = new ApiTestClient(_factory);
        using var secondClient = new ApiTestClient(_factory);
        await firstClient.RegisterAsync($"a{suffix}");
        await secondClient.RegisterAsync($"b{suffix}");
        await firstClient.PostAsync($"/api/v1/cart/items/{product.ProductCode}", new { quantity = 2 });
        await secondClient.PostAsync($"/api/v1/cart/items/{product.ProductCode}", new { quantity = 4 });

        // Act
        var firstCart = await ApiTestClient.ReadEnvelopeAsync<CartDto>(await firstClient.GetAsync("/api/v1/cart"));
        var secondCart = await ApiTestClient.ReadEnvelopeAsync<CartDto>(await secondClient.GetAsync("/api/v1/cart"));

        // Assert
        Assert.Equal(2, firstCart.Data?.TotalQuantity);
        Assert.Equal(4, secondCart.Data?.TotalQuantity);
    }
}