namespace WillyShop.Logging;

internal static partial class ApplicationLog
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "Refresh token reuse detected for user {UserId}; active tokens in the family were revoked")]
    public static partial void RefreshTokenReuseDetected(this ILogger logger, string userId);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Order {OrderId} created for user {UserId} with {ItemCount} items")]
    public static partial void OrderCreated(this ILogger logger, int orderId, string userId, int itemCount);

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Information,
        Message = "Product {ProductCode} created by actor {ActorUserId}")]
    public static partial void ProductCreated(this ILogger logger, string productCode, string? actorUserId);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Information,
        Message = "Product {ProductCode} updated by actor {ActorUserId}")]
    public static partial void ProductUpdated(this ILogger logger, string productCode, string? actorUserId);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Information,
        Message = "Product {ProductCode} deleted by actor {ActorUserId}")]
    public static partial void ProductDeleted(this ILogger logger, string productCode, string? actorUserId);

    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Information,
        Message = "Administrative user {TargetUserId} created with role {AssignedRole} by actor {ActorUserId}")]
    public static partial void AdministrativeUserCreated(
        this ILogger logger,
        string targetUserId,
        string assignedRole,
        string? actorUserId);

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Information,
        Message = "Administrative user {TargetUserId} updated with role {AssignedRole} by actor {ActorUserId}; password changed: {PasswordChanged}")]
    public static partial void AdministrativeUserUpdated(
        this ILogger logger,
        string targetUserId,
        string assignedRole,
        string? actorUserId,
        bool passwordChanged);

    [LoggerMessage(
        EventId = 4003,
        Level = LogLevel.Information,
        Message = "Administrative user {TargetUserId} deleted by actor {ActorUserId}")]
    public static partial void AdministrativeUserDeleted(
        this ILogger logger,
        string targetUserId,
        string? actorUserId);

    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Information,
        Message = "Created {NotificationCount} back-in-stock notifications for product {ProductCode}")]
    public static partial void BackInStockNotificationsCreated(
        this ILogger logger,
        int notificationCount,
        string productCode);

    [LoggerMessage(
        EventId = 9001,
        Level = LogLevel.Error,
        Message = "Unhandled exception while processing {RequestMethod} {RequestPath} for user {UserId} with trace {TraceId} and span {SpanId}")]
    public static partial void UnhandledRequestException(
        this ILogger logger,
        Exception exception,
        string requestMethod,
        string requestPath,
        string? userId,
        string traceId,
        string? spanId);

    [LoggerMessage(
        EventId = 9002,
        Level = LogLevel.Error,
        Message = "Database initialization and seed operation failed; required seed data may be unavailable")]
    public static partial void DatabaseInitializationFailed(this ILogger logger, Exception exception);
}