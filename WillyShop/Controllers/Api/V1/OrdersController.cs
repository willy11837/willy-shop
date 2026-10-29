using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WillyShop.Contracts;
using WillyShop.Models;
using WillyShop.Services;

namespace WillyShop.Controllers.Api.V1;

[ApiController]
[Authorize]
[Route("api/v1/orders")]
public sealed class OrdersController : ApiControllerBase
{
    private readonly OrderApplicationService _orders;

    public OrdersController(OrderApplicationService orders) => _orders = orders;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> GetAll(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return ApiSuccess<IReadOnlyList<OrderDto>>(await _orders.GetOrdersAsync(userId, cancellationToken));
    }

    [HttpGet("{orderId:int}")]
    public async Task<ActionResult<OrderDto>> Get(int orderId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var canManage = User.IsInRole(SystemRoles.System) || User.IsInRole(SystemRoles.Admin) || User.IsInRole(SystemRoles.Store);
        var order = await _orders.GetOrderAsync(orderId, userId, canManage, cancellationToken);
        return order is null
            ? ApiFailure("找不到指定的訂單。", StatusCodes.Status404NotFound)
            : ApiSuccess(order);
    }
    [HttpPost("checkout")]
    public async Task<ActionResult<OrderDto>> Checkout(CancellationToken cancellationToken)
    {
        try
        {
            return ApiSuccess(await _orders.CheckoutAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, cancellationToken), "訂單建立成功。");
        }
        catch (InvalidOperationException exception)
        {
            return ApiFailure(exception.Message, StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("{orderId:int}/cancel")]
    public Task<ActionResult<OrderDto>> Cancel(int orderId, CancellationToken cancellationToken)
        => ExecuteTransitionAsync(() => _orders.CancelAsync(orderId, UserId(), cancellationToken), "訂單已取消。");

    [HttpPost("{orderId:int}/complete")]
    public Task<ActionResult<OrderDto>> Complete(int orderId, CancellationToken cancellationToken)
        => ExecuteTransitionAsync(() => _orders.CompleteAsync(orderId, UserId(), cancellationToken), "訂單已完成。");

    [HttpPost("{orderId:int}/return-request")]
    public Task<ActionResult<OrderDto>> RequestReturn(int orderId, ReturnRequest request, CancellationToken cancellationToken)
        => ExecuteTransitionAsync(
            () => _orders.RequestReturnAsync(orderId, UserId(), request.Reason, cancellationToken),
            "退貨申請已送出。");

    private async Task<ActionResult<OrderDto>> ExecuteTransitionAsync(Func<Task<OrderDto>> operation, string message)
    {
        try { return ApiSuccess(await operation(), message); }
        catch (KeyNotFoundException exception) { return ApiFailure(exception.Message, StatusCodes.Status404NotFound); }
        catch (InvalidOperationException exception) { return ApiFailure(exception.Message, StatusCodes.Status409Conflict); }
    }

    private string UserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("登入憑證缺少使用者識別碼。");
}