using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using WillyShop.Contracts;
using WillyShop.Services;

namespace WillyShop.Controllers.Api.V1;

[ApiController]
[Route("api/v1/cart")]
public sealed class CartController : ApiControllerBase
{
    private readonly CartApplicationService _cart;

    public CartController(CartApplicationService cart) => _cart = cart;

    [HttpGet]
    public async Task<ActionResult<CartDto>> Get() => ApiSuccess(await _cart.GetAsync(HttpContext, User.FindFirstValue(ClaimTypes.NameIdentifier)));
    [HttpPost("items/{productCode}")]
    public async Task<ActionResult<CartDto>> Add(string productCode, CartQuantityRequest request)
    {
        try
        {
            await _cart.AddAsync(HttpContext, User.FindFirstValue(ClaimTypes.NameIdentifier), productCode, request.Quantity);
            return ApiSuccess(await _cart.GetAsync(HttpContext, User.FindFirstValue(ClaimTypes.NameIdentifier)), "商品已加入購物車。");
        }
        catch (KeyNotFoundException exception) { return ApiFailure(exception.Message, StatusCodes.Status404NotFound); }
        catch (InvalidOperationException exception) { return ApiFailure(exception.Message, StatusCodes.Status400BadRequest); }
    }
    [HttpPut("items/{productCode}")]
    public async Task<ActionResult<CartDto>> Update(string productCode, CartQuantityRequest request)
    {
        try
        {
            await _cart.UpdateAsync(HttpContext, User.FindFirstValue(ClaimTypes.NameIdentifier), productCode, request.Quantity);
            return ApiSuccess(await _cart.GetAsync(HttpContext, User.FindFirstValue(ClaimTypes.NameIdentifier)), "購物車數量已更新。");
        }
        catch (KeyNotFoundException exception) { return ApiFailure(exception.Message, StatusCodes.Status404NotFound); }
        catch (InvalidOperationException exception) { return ApiFailure(exception.Message, StatusCodes.Status400BadRequest); }
    }
    [HttpDelete("items/{productCode}")]
    public async Task<ActionResult<CartDto>> Remove(string productCode)
    {
        await _cart.RemoveAsync(HttpContext, User.FindFirstValue(ClaimTypes.NameIdentifier), productCode);
        return ApiSuccess(await _cart.GetAsync(HttpContext, User.FindFirstValue(ClaimTypes.NameIdentifier)), "商品已從購物車移除。");
    }
}