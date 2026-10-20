using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using WillyShop.Contracts;
using WillyShop.Services;

namespace WillyShop.Controllers.Api.V1;

[ApiController]
[Route("api/v1/cart")]
public sealed class CartController : ControllerBase
{
    private readonly CartApplicationService _cart;

    public CartController(CartApplicationService cart) => _cart = cart;

    [HttpGet]
    public async Task<ActionResult<CartDto>> Get() => Ok(await _cart.GetAsync(HttpContext, User.FindFirstValue(ClaimTypes.NameIdentifier)));

    [ValidateAntiForgeryToken]
    [HttpPost("items/{productCode}")]
    public async Task<ActionResult<CartDto>> Add(string productCode, CartQuantityRequest request)
    {
        try
        {
            await _cart.AddAsync(HttpContext, User.FindFirstValue(ClaimTypes.NameIdentifier), productCode, request.Quantity);
            return Ok(await _cart.GetAsync(HttpContext, User.FindFirstValue(ClaimTypes.NameIdentifier)));
        }
        catch (KeyNotFoundException exception) { return NotFound(Problem(exception.Message, statusCode: 404)); }
        catch (InvalidOperationException exception) { return BadRequest(Problem(exception.Message, statusCode: 400)); }
    }

    [ValidateAntiForgeryToken]
    [HttpPut("items/{productCode}")]
    public async Task<ActionResult<CartDto>> Update(string productCode, CartQuantityRequest request)
    {
        try
        {
            await _cart.UpdateAsync(HttpContext, User.FindFirstValue(ClaimTypes.NameIdentifier), productCode, request.Quantity);
            return Ok(await _cart.GetAsync(HttpContext, User.FindFirstValue(ClaimTypes.NameIdentifier)));
        }
        catch (KeyNotFoundException exception) { return NotFound(Problem(exception.Message, statusCode: 404)); }
        catch (InvalidOperationException exception) { return BadRequest(Problem(exception.Message, statusCode: 400)); }
    }

    [ValidateAntiForgeryToken]
    [HttpDelete("items/{productCode}")]
    public async Task<ActionResult<CartDto>> Remove(string productCode)
    {
        await _cart.RemoveAsync(HttpContext, User.FindFirstValue(ClaimTypes.NameIdentifier), productCode);
        return Ok(await _cart.GetAsync(HttpContext, User.FindFirstValue(ClaimTypes.NameIdentifier)));
    }
}