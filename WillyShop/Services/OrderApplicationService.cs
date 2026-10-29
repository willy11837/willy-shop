using System.Data;
using Microsoft.EntityFrameworkCore;
using WillyShop.Contracts;
using WillyShop.Data;
using WillyShop.Logging;
using WillyShop.Models;

namespace WillyShop.Services;

public sealed class OrderApplicationService
{
    private readonly ApplicationDbContext _db;
    private readonly NotificationService _notifications;
    private readonly ILogger<OrderApplicationService> _logger;

    public OrderApplicationService(ApplicationDbContext db, NotificationService notifications, ILogger<OrderApplicationService> logger)
    {
        _db = db;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<OrderDto> CheckoutAsync(string userId, CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var cart = await _db.UserCartItems.Where(item => item.UserId == userId).ToListAsync(cancellationToken);
        if (cart.Count == 0) throw new InvalidOperationException("購物車是空的。");

        var codes = cart.Select(item => item.ProductCode).Distinct().ToList();
        var products = await _db.Products.Where(product => codes.Contains(product.ProductCode))
            .ToDictionaryAsync(product => product.ProductCode, cancellationToken);
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
            TotalAmount = cart.Sum(item => products[item.ProductCode].EffectivePrice * item.Quantity),
            CreatedAt = DateTime.UtcNow,
            Status = OrderStatuses.Pending
        };
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);

        var orderItems = cart.Select(item =>
        {
            var product = products[item.ProductCode];
            var unitPrice = product.EffectivePrice;
            product.StockQuantity -= item.Quantity;
            if (product.StockQuantity == 0) product.Status = ProductStatus.數量不足;
            product.ModifiedAt = DateTime.UtcNow;
            product.ModifiedBy = "Checkout";
            return new OrderItem
            {
                OrderId = order.Id,
                ProductCode = item.ProductCode,
                ProductName = product.Name,
                UnitPrice = unitPrice,
                Quantity = item.Quantity,
                Subtotal = unitPrice * item.Quantity
            };
        }).ToList();

        _db.OrderItems.AddRange(orderItems);
        _db.UserCartItems.RemoveRange(cart);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        _logger.OrderCreated(order.Id, userId, orderItems.Count);
        return Map(order, orderItems);
    }

    public async Task<List<OrderDto>> GetOrdersAsync(string userId, CancellationToken cancellationToken)
    {
        var orders = await _db.Orders.AsNoTracking().Where(order => order.UserId == userId)
            .OrderByDescending(order => order.CreatedAt).ToListAsync(cancellationToken);
        var ids = orders.Select(order => order.Id).ToList();
        var items = await _db.OrderItems.AsNoTracking().Where(item => ids.Contains(item.OrderId)).ToListAsync(cancellationToken);
        return orders.Select(order => Map(order, items.Where(item => item.OrderId == order.Id).ToList())).ToList();
    }

    public async Task<OrderDto?> GetOrderAsync(int orderId, string userId, bool canManage, CancellationToken cancellationToken)
    {
        var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(value => value.Id == orderId, cancellationToken);
        if (order is null || (!canManage && order.UserId != userId)) return null;
        var items = await _db.OrderItems.AsNoTracking().Where(item => item.OrderId == orderId).ToListAsync(cancellationToken);
        return Map(order, items);
    }

    public async Task<PagedResult<AdminOrderDto>> SearchAdminOrdersAsync(AdminOrderQuery request, CancellationToken cancellationToken)
    {
        var query = from order in _db.Orders.AsNoTracking()
                    join user in _db.Users.AsNoTracking() on order.UserId equals user.Id
                    select new { Order = order, User = user };

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            var matchingOrderIds = _db.OrderItems.AsNoTracking()
                .Where(item => item.ProductCode.Contains(keyword) || item.ProductName.Contains(keyword))
                .Select(item => item.OrderId);
            query = query.Where(value => value.Order.Id.ToString().Contains(keyword)
                || (value.User.UserName != null && value.User.UserName.Contains(keyword))
                || (value.User.DisplayName != null && value.User.DisplayName.Contains(keyword))
                || (value.User.Email != null && value.User.Email.Contains(keyword))
                || matchingOrderIds.Contains(value.Order.Id));
        }
        if (!string.IsNullOrWhiteSpace(request.Status)) query = query.Where(value => value.Order.Status == request.Status);
        if (request.CreatedFrom.HasValue) query = query.Where(value => value.Order.CreatedAt >= request.CreatedFrom.Value);
        if (request.CreatedTo.HasValue) query = query.Where(value => value.Order.CreatedAt <= request.CreatedTo.Value);

        query = (request.SortBy, request.SortDirection) switch
        {
            ("createdAt", "asc") => query.OrderBy(value => value.Order.CreatedAt).ThenBy(value => value.Order.Id),
            ("totalAmount", "asc") => query.OrderBy(value => value.Order.TotalAmount).ThenBy(value => value.Order.Id),
            ("totalAmount", "desc") => query.OrderByDescending(value => value.Order.TotalAmount).ThenByDescending(value => value.Order.Id),
            _ => query.OrderByDescending(value => value.Order.CreatedAt).ThenByDescending(value => value.Order.Id)
        };

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(cancellationToken);
        var ids = rows.Select(value => value.Order.Id).ToList();
        var items = await _db.OrderItems.AsNoTracking().Where(item => ids.Contains(item.OrderId)).ToListAsync(cancellationToken);
        var result = rows.Select(value => MapAdmin(value.Order, value.User,
            items.Where(item => item.OrderId == value.Order.Id).ToList())).ToList();
        return new PagedResult<AdminOrderDto>(result, request.Page, request.PageSize, totalCount);
    }

    public async Task<AdminOrderDto?> GetAdminOrderAsync(int orderId, CancellationToken cancellationToken)
    {
        var row = await (from order in _db.Orders.AsNoTracking()
                         join user in _db.Users.AsNoTracking() on order.UserId equals user.Id
                         where order.Id == orderId
                         select new { Order = order, User = user }).FirstOrDefaultAsync(cancellationToken);
        if (row is null) return null;
        var items = await _db.OrderItems.AsNoTracking().Where(item => item.OrderId == orderId).ToListAsync(cancellationToken);
        return MapAdmin(row.Order, row.User, items);
    }

    public Task<OrderDto> CancelAsync(int orderId, string userId, CancellationToken cancellationToken)
        => ChangeStatusAsync(orderId, userId, OrderStatuses.Pending, OrderStatuses.Cancelled, cancellationToken,
            order => order.CancelledAt = DateTime.UtcNow, true);

    public Task<OrderDto> CancelAsManagerAsync(int orderId, CancellationToken cancellationToken)
        => ChangeStatusAsync(orderId, null, OrderStatuses.Pending, OrderStatuses.Cancelled, cancellationToken,
            order => order.CancelledAt = DateTime.UtcNow, true);

    public Task<OrderDto> CompleteAsync(int orderId, string userId, CancellationToken cancellationToken)
        => ChangeStatusAsync(orderId, userId, OrderStatuses.Shipped, OrderStatuses.Completed, cancellationToken,
            order => order.CompletedAt = DateTime.UtcNow);

    public Task<OrderDto> RequestReturnAsync(int orderId, string userId, string reason, CancellationToken cancellationToken)
        => ChangeStatusAsync(orderId, userId, OrderStatuses.Completed, OrderStatuses.ReturnRequested, cancellationToken,
            order =>
            {
                order.ReturnRequestedAt = DateTime.UtcNow;
                order.ReturnReason = reason.Trim();
                order.ReturnReviewedAt = null;
                order.ReturnReviewedBy = null;
                order.ReturnReviewNote = null;
            });

    public Task<OrderDto> ShipAsync(int orderId, CancellationToken cancellationToken)
        => ChangeStatusAsync(orderId, null, OrderStatuses.Pending, OrderStatuses.Shipped, cancellationToken,
            order => order.ShippedAt = DateTime.UtcNow);

    public Task<OrderDto> ApproveReturnAsync(int orderId, string actorUserId, string? note, CancellationToken cancellationToken)
        => ChangeStatusAsync(orderId, null, OrderStatuses.ReturnRequested, OrderStatuses.ReturnApproved, cancellationToken,
            order => SetReview(order, actorUserId, note));

    public Task<OrderDto> RejectReturnAsync(int orderId, string actorUserId, string? note, CancellationToken cancellationToken)
        => ChangeStatusAsync(orderId, null, OrderStatuses.ReturnRequested, OrderStatuses.Completed, cancellationToken,
            order => SetReview(order, actorUserId, note));

    public Task<OrderDto> CompleteRefundAsync(int orderId, string actorUserId, string? note, CancellationToken cancellationToken)
        => ChangeStatusAsync(orderId, null, OrderStatuses.ReturnApproved, OrderStatuses.Refunded, cancellationToken,
            order =>
            {
                order.RefundCompletedAt = DateTime.UtcNow;
                order.RefundProcessedBy = actorUserId;
                order.RefundAmount = order.TotalAmount;
                order.RefundNote = NormalizeNote(note);
            }, true);

    private async Task<OrderDto> ChangeStatusAsync(
        int orderId,
        string? ownerUserId,
        string expectedStatus,
        string nextStatus,
        CancellationToken cancellationToken,
        Action<Order> apply,
        bool restockInventory = false)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var order = await _db.Orders.FirstOrDefaultAsync(value => value.Id == orderId, cancellationToken)
            ?? throw new KeyNotFoundException("找不到指定的訂單。");
        if (ownerUserId is not null && order.UserId != ownerUserId) throw new KeyNotFoundException("找不到指定的訂單。");
        if (order.Status != expectedStatus)
            throw new InvalidOperationException($"目前訂單狀態為「{OrderStatuses.DisplayName(order.Status)}」，無法執行此操作。");

        var items = await _db.OrderItems.Where(item => item.OrderId == orderId).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        apply(order);
        order.Status = nextStatus;
        order.ModifiedAt = now;
        if (restockInventory) await RestockInventoryAsync(order, items, now, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(order, items);
    }

    private async Task RestockInventoryAsync(Order order, IReadOnlyList<OrderItem> items, DateTime now, CancellationToken cancellationToken)
    {
        if (order.InventoryRestockedAt.HasValue) throw new InvalidOperationException("此訂單的商品庫存已回補。");
        var codes = items.Select(item => item.ProductCode).Distinct().ToList();
        var products = await _db.Products.Where(product => codes.Contains(product.ProductCode))
            .ToDictionaryAsync(product => product.ProductCode, cancellationToken);
        foreach (var item in items)
        {
            if (!products.TryGetValue(item.ProductCode, out var product)) continue;
            var wasOutOfStock = NotificationService.IsOutOfStock(product);
            product.StockQuantity += item.Quantity;
            if (product.Status == ProductStatus.數量不足) product.Status = ProductStatus.可銷售;
            product.ModifiedAt = now;
            product.ModifiedBy = "OrderRestock";
            if (wasOutOfStock && NotificationService.IsPurchasable(product))
                await _notifications.NotifyBackInStockAsync(product, cancellationToken);
        }
        order.InventoryRestockedAt = now;
    }

    private static void SetReview(Order order, string actorUserId, string? note)
    {
        order.ReturnReviewedAt = DateTime.UtcNow;
        order.ReturnReviewedBy = actorUserId;
        order.ReturnReviewNote = NormalizeNote(note);
    }

    private static OrderDto Map(Order order, IReadOnlyList<OrderItem> items) => new(
        order.Id, order.TotalAmount, order.CreatedAt, order.Status, OrderStatuses.DisplayName(order.Status),
        order.ModifiedAt, order.ShippedAt, order.CompletedAt, order.CancelledAt, order.ReturnRequestedAt,
        order.ReturnReason, order.ReturnReviewedAt, order.ReturnReviewNote, order.RefundCompletedAt,
        order.RefundAmount, order.RefundNote, items.Select(MapItem).ToList());

    private static AdminOrderDto MapAdmin(Order order, ApplicationUser user, IReadOnlyList<OrderItem> items) => new(
        order.Id, order.UserId, user.UserName ?? string.Empty, user.DisplayName, user.Email, order.TotalAmount,
        order.CreatedAt, order.Status, OrderStatuses.DisplayName(order.Status), order.ModifiedAt, order.ShippedAt,
        order.CompletedAt, order.CancelledAt, order.ReturnRequestedAt, order.ReturnReason, order.ReturnReviewedAt,
        order.ReturnReviewedBy, order.ReturnReviewNote, order.RefundCompletedAt, order.RefundProcessedBy,
        order.RefundAmount, order.RefundNote, items.Select(MapItem).ToList());

    private static OrderItemDto MapItem(OrderItem item)
        => new(item.ProductCode, item.ProductName, item.UnitPrice, item.Quantity, item.Subtotal);

    private static string? NormalizeNote(string? note) => string.IsNullOrWhiteSpace(note) ? null : note.Trim();
}