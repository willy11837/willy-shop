using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WillyShop.Contracts;
using WillyShop.Models;
using WillyShop.Services;

namespace WillyShop.Controllers.Api.V1;

[ApiController]
[Authorize(Roles = SystemRoles.OrderManagers)]
[Route("api/v1/admin/orders")]
public sealed class AdminOrdersController : ApiControllerBase
{
    private readonly OrderApplicationService _orders;

    public AdminOrdersController(OrderApplicationService orders) => _orders = orders;

    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminOrderDto>>> GetAll(
        [FromQuery] AdminOrderQuery query,
        CancellationToken cancellationToken)
    {
        if (query.CreatedFrom > query.CreatedTo)
            return ApiValidationFailure(new Dictionary<string, string[]>
            {
                [nameof(query.CreatedTo)] = ["建立迄日不得早於建立起日。"]
            });
        if (!string.IsNullOrWhiteSpace(query.Status) && !OrderStatuses.All.Contains(query.Status))
            return ApiValidationFailure(new Dictionary<string, string[]>
            {
                [nameof(query.Status)] = ["不支援的訂單狀態。"]
            });
        return ApiSuccess(await _orders.SearchAdminOrdersAsync(query, cancellationToken));
    }

    [HttpGet("{orderId:int}")]
    public async Task<ActionResult<AdminOrderDto>> Get(int orderId, CancellationToken cancellationToken)
    {
        var order = await _orders.GetAdminOrderAsync(orderId, cancellationToken);
        return order is null
            ? ApiFailure("找不到指定的訂單。", StatusCodes.Status404NotFound)
            : ApiSuccess(order);
    }

    [HttpPost("{orderId:int}/ship")]
    public Task<ActionResult<OrderDto>> Ship(int orderId, CancellationToken cancellationToken)
        => ExecuteAsync(() => _orders.ShipAsync(orderId, cancellationToken), "訂單已標記為寄送中。");

    [HttpPost("{orderId:int}/cancel")]
    public Task<ActionResult<OrderDto>> Cancel(int orderId, CancellationToken cancellationToken)
        => ExecuteAsync(() => _orders.CancelAsManagerAsync(orderId, cancellationToken), "訂單已取消。");

    [HttpPost("{orderId:int}/approve-return")]
    public Task<ActionResult<OrderDto>> ApproveReturn(int orderId, ReviewReturnRequest request, CancellationToken cancellationToken)
        => ExecuteAsync(() => _orders.ApproveReturnAsync(orderId, ActorUserId(), request.Note, cancellationToken), "退貨申請已核准。");

    [HttpPost("{orderId:int}/reject-return")]
    public Task<ActionResult<OrderDto>> RejectReturn(int orderId, ReviewReturnRequest request, CancellationToken cancellationToken)
        => ExecuteAsync(() => _orders.RejectReturnAsync(orderId, ActorUserId(), request.Note, cancellationToken), "退貨申請已駁回。");

    [HttpPost("{orderId:int}/complete-refund")]
    public Task<ActionResult<OrderDto>> CompleteRefund(int orderId, CompleteRefundRequest request, CancellationToken cancellationToken)
        => ExecuteAsync(() => _orders.CompleteRefundAsync(orderId, ActorUserId(), request.Note, cancellationToken), "人工退款已完成並記錄。");

    private async Task<ActionResult<OrderDto>> ExecuteAsync(Func<Task<OrderDto>> operation, string message)
    {
        try { return ApiSuccess(await operation(), message); }
        catch (KeyNotFoundException exception) { return ApiFailure(exception.Message, StatusCodes.Status404NotFound); }
        catch (InvalidOperationException exception) { return ApiFailure(exception.Message, StatusCodes.Status409Conflict); }
    }

    private string ActorUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("登入憑證缺少使用者識別碼。");
}