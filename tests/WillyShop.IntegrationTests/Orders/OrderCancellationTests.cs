using System.Net;
using Microsoft.EntityFrameworkCore;
using WillyShop.IntegrationTests.Infrastructure;
using WillyShop.Models;

namespace WillyShop.IntegrationTests.Orders;

[Collection(IntegrationTestCollection.Name)]
public sealed class OrderCancellationTests
{
    private readonly WillyShopWebApplicationFactory _factory;

    public OrderCancellationTests(WillyShopWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task CancelAsync_同一訂單重複取消_第二次回傳409且庫存只回補一次()
    {
        // Arrange
        var suffix = TestData.NewSuffix();
        var product = TestData.CreateProduct(suffix, stockQuantity: 1);
        await TestData.AddProductAsync(_factory, product);
        using var client = new ApiTestClient(_factory);
        var user = await client.RegisterAsync(suffix);
        await TestData.AddCartItemAsync(_factory, user.Id, product.ProductCode, 1);
        var checkoutResponse = await client.PostAsync("/api/v1/orders/checkout");
        checkoutResponse.EnsureSuccessStatusCode();
        var checkout = await ApiTestClient.ReadEnvelopeAsync<WillyShop.Contracts.OrderDto>(checkoutResponse);
        var orderId = checkout.Data?.Id ?? throw new InvalidOperationException("結帳未回傳訂單識別碼。");

        // Act
        var firstCancellation = await client.PostAsync($"/api/v1/orders/{orderId}/cancel");
        var secondCancellation = await client.PostAsync($"/api/v1/orders/{orderId}/cancel");

        // Assert
        Assert.Equal(HttpStatusCode.OK, firstCancellation.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondCancellation.StatusCode);
        await using var db = _factory.CreateDbContext();
        var storedProduct = await db.Products.AsNoTracking().SingleAsync(
            value => value.ProductCode == product.ProductCode,
            TestContext.Current.CancellationToken);
        var storedOrder = await db.Orders.AsNoTracking().SingleAsync(
            value => value.Id == orderId,
            TestContext.Current.CancellationToken);
        Assert.Equal(1, storedProduct.StockQuantity);
        Assert.Equal(ProductStatus.可銷售, storedProduct.Status);
        Assert.Equal(OrderStatuses.Cancelled, storedOrder.Status);
        Assert.NotNull(storedOrder.InventoryRestockedAt);
    }
}