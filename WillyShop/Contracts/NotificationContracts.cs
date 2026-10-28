using WillyShop.Models;

namespace WillyShop.Contracts;

public sealed record BackInStockSubscriptionDto(bool IsSubscribed, DateTime? CreatedAt);

public sealed record NotificationDto(
    long Id,
    NotificationType Type,
    string TypeName,
    string Title,
    string Message,
    string? ProductCode,
    string? TargetUrl,
    DateTime CreatedAt,
    DateTime? ReadAt);

public sealed record UnreadNotificationCountDto(int Count);