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
public sealed class OrdersController : ControllerBase
{
    private readonly OrderApplicationService _orders;

    public OrdersController(OrderApplicationService orders) => _orders = orders;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> GetAll()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _orders.GetOrdersAsync(userId));
    }

    [HttpGet("{orderId:int}")]
    public async Task<ActionResult<OrderDto>> Get(int orderId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var canManage = User.IsInRole(SystemRoles.System) || User.IsInRole(SystemRoles.Admin);
        var order = await _orders.GetOrderAsync(orderId, userId, canManage);
        return order is null ? NotFound() : Ok(order);
    }

    [ValidateAntiForgeryToken]
    [HttpPost("checkout")]
    public async Task<ActionResult<OrderDto>> Checkout()
    {
        try
        {
            return Ok(await _orders.CheckoutAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(Problem(exception.Message, statusCode: 400));
        }
    }
}