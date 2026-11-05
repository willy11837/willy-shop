using System.Net;
using Microsoft.EntityFrameworkCore;
using WillyShop.IntegrationTests.Infrastructure;
using WillyShop.Models;

namespace WillyShop.IntegrationTests.Orders;

[Collection(IntegrationTestCollection.Name)]
public sealed class OrderAuthorizationAndStateTests
{
    private readonly WillyShopWebApplicationFactory _factory;

    public OrderAuthorizationAndStateTests(WillyShopWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Get_查詢其他使用者訂單_回傳404不洩漏訂單()
    {
        // Arrange
        var suffix = TestData.NewSuffix();
        using var ownerClient = new ApiTestClient(_factory);
        using var otherClient = new ApiTestClient(_factory);
        var owner = await ownerClient.RegisterAsync($"a{suffix}");
        await otherClient.RegisterAsync($"b{suffix}");
        var order = await TestData.AddOrderAsync(_factory, owner.Id);

        // Act
        var response = await otherClient.GetAsync($"/api/v1/orders/{order.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CancelAsync_取消其他使用者訂單_回傳404且狀態不變()
    {
        // Arrange
        var suffix = TestData.NewSuffix();
        using var ownerClient = new ApiTestClient(_factory);
        using var otherClient = new ApiTestClient(_factory);
        var owner = await ownerClient.RegisterAsync($"a{suffix}");
        await otherClient.RegisterAsync($"b{suffix}");
        var order = await TestData.AddOrderAsync(_factory, owner.Id);

        // Act
        var response = await otherClient.PostAsync($"/api/v1/orders/{order.Id}/cancel");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await using var db = _factory.CreateDbContext();
        Assert.Equal(OrderStatuses.Pending,
            (await db.Orders.AsNoTracking().SingleAsync(
                value => value.Id == order.Id,
                TestContext.Current.CancellationToken)).Status);
    }

    [Fact]
    public async Task CompleteAsync_Pending訂單直接完成_回傳409且狀態不變()
    {
        // Arrange
        var suffix = TestData.NewSuffix();
        using var client = new ApiTestClient(_factory);
        var user = await client.RegisterAsync(suffix);
        var order = await TestData.AddOrderAsync(_factory, user.Id);

        // Act
        var response = await client.PostAsync($"/api/v1/orders/{order.Id}/complete");

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var db = _factory.CreateDbContext();
        Assert.Equal(OrderStatuses.Pending,
            (await db.Orders.AsNoTracking().SingleAsync(
                value => value.Id == order.Id,
                TestContext.Current.CancellationToken)).Status);
    }
}