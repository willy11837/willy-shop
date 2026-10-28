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
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> GetAll()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return ApiSuccess(await _orders.GetOrdersAsync(userId));
    }

    [HttpGet("{orderId:int}")]
    public async Task<ActionResult<OrderDto>> Get(int orderId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var canManage = User.IsInRole(SystemRoles.System) || User.IsInRole(SystemRoles.Admin);
        var order = await _orders.GetOrderAsync(orderId, userId, canManage);
        return order is null
            ? ApiFailure("找不到指定的訂單。", StatusCodes.Status404NotFound)
            : ApiSuccess(order);
    }
    [HttpPost("checkout")]
    public async Task<ActionResult<OrderDto>> Checkout()
    {
        try
        {
            return ApiSuccess(await _orders.CheckoutAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!), "訂單建立成功。");
        }
        catch (InvalidOperationException exception)
        {
            return ApiFailure(exception.Message, StatusCodes.Status400BadRequest);
        }
    }
}