using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WillyShop.Contracts;
using WillyShop.Services;

namespace WillyShop.Controllers.Api.V1;

[ApiController]
[Authorize]
[Route("api/v1/notifications")]
public sealed class NotificationsController : ApiControllerBase
{
    private readonly NotificationService _notifications;

    public NotificationsController(NotificationService notifications) => _notifications = notifications;

    [HttpGet]
    public async Task<ActionResult<PagedResult<NotificationDto>>> GetAll(
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default)
        => ApiSuccess(await _notifications.GetNotificationsAsync(UserId(), page, pageSize, cancellationToken));

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadNotificationCountDto>> GetUnreadCount(CancellationToken cancellationToken)
        => ApiSuccess(new UnreadNotificationCountDto(
            await _notifications.GetUnreadCountAsync(UserId(), cancellationToken)));

    [HttpPut("{notificationId:long}/read")]
    public async Task<IActionResult> MarkRead(long notificationId, CancellationToken cancellationToken)
    {
        var result = await _notifications.MarkReadAsync(UserId(), notificationId, cancellationToken);
        return result switch
        {
            null => ApiFailure("找不到指定的通知。", StatusCodes.Status404NotFound),
            false => ApiFailure("目前帳號無法存取此通知。", StatusCodes.Status403Forbidden),
            true => ApiSuccess<object?>(null, "通知已標示為已讀。")
        };
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var count = await _notifications.MarkAllReadAsync(UserId(), cancellationToken);
        return ApiSuccess(new UnreadNotificationCountDto(0), $"已將 {count} 則通知標示為已讀。");
    }

    private string UserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("登入憑證缺少使用者識別碼。");
}