using Microsoft.EntityFrameworkCore;
using WillyShop.Contracts;
using WillyShop.Data;
using WillyShop.Models;

namespace WillyShop.Services;

public sealed class NotificationService
{
    private readonly ApplicationDbContext _db;

    public NotificationService(ApplicationDbContext db) => _db = db;

    public async Task<BackInStockSubscriptionDto> GetSubscriptionAsync(
        string userId,
        string productCode,
        CancellationToken cancellationToken)
    {
        var subscription = await _db.BackInStockSubscriptions.AsNoTracking()
            .Where(value => value.UserId == userId
                && value.ProductCode == productCode
                && value.NotifiedAt == null)
            .Select(value => new BackInStockSubscriptionDto(true, value.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);
        return subscription ?? new BackInStockSubscriptionDto(false, null);
    }

    public async Task<BackInStockSubscriptionDto?> SubscribeAsync(
        string userId,
        string productCode,
        CancellationToken cancellationToken)
    {
        var product = await _db.Products.AsNoTracking()
            .FirstOrDefaultAsync(value => value.ProductCode == productCode, cancellationToken);
        if (product is null) return null;
        if (!IsOutOfStock(product))
            throw new InvalidOperationException("此商品目前不是缺貨狀態。");

        var existing = await GetSubscriptionAsync(userId, productCode, cancellationToken);
        if (existing.IsSubscribed) return existing;

        var entity = new BackInStockSubscription
        {
            UserId = userId,
            ProductCode = productCode,
            CreatedAt = DateTime.UtcNow
        };
        _db.BackInStockSubscriptions.Add(entity);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _db.Entry(entity).State = EntityState.Detached;
            var concurrentSubscription = await GetSubscriptionAsync(userId, productCode, cancellationToken);
            if (concurrentSubscription.IsSubscribed) return concurrentSubscription;
            throw;
        }
        return new BackInStockSubscriptionDto(true, entity.CreatedAt);
    }

    public async Task<bool?> UnsubscribeAsync(
        string userId,
        string productCode,
        CancellationToken cancellationToken)
    {
        if (!await _db.Products.AnyAsync(value => value.ProductCode == productCode, cancellationToken))
            return null;

        var subscriptions = await _db.BackInStockSubscriptions
            .Where(value => value.UserId == userId
                && value.ProductCode == productCode
                && value.NotifiedAt == null)
            .ToListAsync(cancellationToken);
        if (subscriptions.Count == 0) return false;

        _db.BackInStockSubscriptions.RemoveRange(subscriptions);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<PagedResult<NotificationDto>> GetNotificationsAsync(
        string userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = _db.UserNotifications.AsNoTracking()
            .Where(value => value.UserId == userId)
            .OrderByDescending(value => value.CreatedAt)
            .ThenByDescending(value => value.Id);
        var totalCount = await query.CountAsync(cancellationToken);
        var notifications = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(value => new NotificationDto(
                value.Id,
                value.Type,
                value.Type.ToString(),
                value.Title,
                value.Message,
                value.ProductCode,
                value.TargetUrl,
                value.CreatedAt,
                value.ReadAt))
            .ToListAsync(cancellationToken);
        return new PagedResult<NotificationDto>(notifications, page, pageSize, totalCount);
    }

    public Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken)
        => _db.UserNotifications.CountAsync(
            value => value.UserId == userId && value.ReadAt == null,
            cancellationToken);

    public async Task<bool?> MarkReadAsync(string userId, long notificationId, CancellationToken cancellationToken)
    {
        var notification = await _db.UserNotifications
            .FirstOrDefaultAsync(value => value.Id == notificationId, cancellationToken);
        if (notification is null) return null;
        if (notification.UserId != userId) return false;
        if (notification.ReadAt is null)
        {
            notification.ReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
        return true;
    }

    public async Task<int> MarkAllReadAsync(string userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return await _db.UserNotifications
            .Where(value => value.UserId == userId && value.ReadAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(value => value.ReadAt, now),
                cancellationToken);
    }

    public async Task NotifyBackInStockAsync(Product product, CancellationToken cancellationToken)
    {
        var subscriptions = await _db.BackInStockSubscriptions
            .Where(value => value.ProductCode == product.ProductCode && value.NotifiedAt == null)
            .ToListAsync(cancellationToken);
        if (subscriptions.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var subscription in subscriptions)
        {
            _db.UserNotifications.Add(new UserNotification
            {
                UserId = subscription.UserId,
                Type = NotificationType.BackInStock,
                Title = $"{product.Name} 已恢復供貨",
                Message = $"您訂閱的商品「{product.Name}」目前已有庫存，可以再次購買。",
                ProductCode = product.ProductCode,
                TargetUrl = $"/products/{Uri.EscapeDataString(product.ProductCode)}",
                CreatedAt = now
            });
            subscription.NotifiedAt = now;
        }
    }

    public static bool IsPurchasable(Product product)
        => product.Status == ProductStatus.可銷售 && product.StockQuantity > 0;

    public static bool IsOutOfStock(Product product)
        => product.Status == ProductStatus.數量不足 || product.StockQuantity <= 0;
}