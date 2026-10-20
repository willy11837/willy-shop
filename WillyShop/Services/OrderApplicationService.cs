using Microsoft.EntityFrameworkCore;
using System.Data;
using WillyShop.Contracts;
using WillyShop.Data;
using WillyShop.Models;

namespace WillyShop.Services;

public sealed class OrderApplicationService
{
    private readonly ApplicationDbContext _db;

    public OrderApplicationService(ApplicationDbContext db) => _db = db;

    public async Task<OrderDto> CheckoutAsync(string userId)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var cart = await _db.UserCartItems.Where(item => item.UserId == userId).ToListAsync();
        if (cart.Count == 0) throw new InvalidOperationException("購物車是空的。");

        var codes = cart.Select(item => item.ProductCode).Distinct().ToList();
        var products = await _db.Products.Where(product => codes.Contains(product.ProductCode)).ToDictionaryAsync(product => product.ProductCode);
        foreach (var item in cart)
        {
            if (!products.TryGetValue(item.ProductCode, out var product))
                throw new InvalidOperationException($"商品 {item.ProductCode} 已不存在。");
            if (product.Status != ProductStatus.可銷售)
                throw new InvalidOperationException($"商品「{product.Name}」目前無法銷售。");
            if (item.Quantity > product.StockQuantity)
                throw new InvalidOperationException($"商品「{product.Name}」庫存不足，目前僅剩 {product.StockQuantity} 件。");
        }

        var order = new Order
        {
            UserId = userId,
            TotalAmount = cart.Sum(item => products[item.ProductCode].Price * item.Quantity),
            CreatedAt = DateTime.UtcNow,
            Status = "Pending"
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        var orderItems = cart.Select(item =>
        {
            var product = products[item.ProductCode];
            product.StockQuantity -= item.Quantity;
            if (product.StockQuantity == 0) product.Status = ProductStatus.數量不足;
            product.ModifiedAt = DateTime.UtcNow;
            product.ModifiedBy = "Checkout";
            return new OrderItem
            {
                OrderId = order.Id,
                ProductCode = item.ProductCode,
                ProductName = product.Name,
                UnitPrice = product.Price,
                Quantity = item.Quantity,
                Subtotal = product.Price * item.Quantity
            };
        }).ToList();

        _db.OrderItems.AddRange(orderItems);
        _db.UserCartItems.RemoveRange(cart);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Map(order, orderItems);
    }

    public async Task<List<OrderDto>> GetOrdersAsync(string userId)
    {
        var orders = await _db.Orders.Where(order => order.UserId == userId).OrderByDescending(order => order.CreatedAt).ToListAsync();
        var ids = orders.Select(order => order.Id).ToList();
        var items = await _db.OrderItems.Where(item => ids.Contains(item.OrderId)).ToListAsync();
        return orders.Select(order => Map(order, items.Where(item => item.OrderId == order.Id).ToList())).ToList();
    }

    public async Task<OrderDto?> GetOrderAsync(int orderId, string userId, bool canManage)
    {
        var order = await _db.Orders.FindAsync(orderId);
        if (order is null || (!canManage && order.UserId != userId)) return null;
        var items = await _db.OrderItems.Where(item => item.OrderId == orderId).ToListAsync();
        return Map(order, items);
    }

    private static OrderDto Map(Order order, IReadOnlyList<OrderItem> items) => new(
        order.Id,
        order.TotalAmount,
        order.CreatedAt,
        order.Status ?? "Pending",
        items.Select(item => new OrderItemDto(item.ProductCode, item.ProductName, item.UnitPrice, item.Quantity, item.Subtotal)).ToList());
}