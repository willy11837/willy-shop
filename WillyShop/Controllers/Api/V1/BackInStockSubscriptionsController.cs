using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WillyShop.Contracts;
using WillyShop.Services;

namespace WillyShop.Controllers.Api.V1;

[ApiController]
[Authorize]
[Route("api/v1/products/{productCode}/back-in-stock-subscription")]
public sealed class BackInStockSubscriptionsController : ApiControllerBase
{
    private readonly NotificationService _notifications;

    public BackInStockSubscriptionsController(NotificationService notifications) => _notifications = notifications;

    [HttpGet]
    public async Task<ActionResult<BackInStockSubscriptionDto>> Get(
        string productCode,
        CancellationToken cancellationToken)
        => ApiSuccess(await _notifications.GetSubscriptionAsync(UserId(), productCode, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<BackInStockSubscriptionDto>> Subscribe(
        string productCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _notifications.SubscribeAsync(UserId(), productCode, cancellationToken);
            return result is null
                ? ApiFailure("找不到指定的商品。", StatusCodes.Status404NotFound)
                : ApiSuccess(result, "已設定到貨通知。", StatusCodes.Status201Created);
        }
        catch (InvalidOperationException exception)
        {
            return ApiFailure(exception.Message, StatusCodes.Status409Conflict);
        }
    }

    [HttpDelete]
    public async Task<IActionResult> Unsubscribe(string productCode, CancellationToken cancellationToken)
    {
        var result = await _notifications.UnsubscribeAsync(UserId(), productCode, cancellationToken);
        return result switch
        {
            null => ApiFailure("找不到指定的商品。", StatusCodes.Status404NotFound),
            false => ApiSuccess<object?>(null, "目前沒有有效的到貨通知訂閱。"),
            true => ApiSuccess<object?>(null, "已取消到貨通知。")
        };
    }

    private string UserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("登入憑證缺少使用者識別碼。");
}