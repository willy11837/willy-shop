using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WillyShop.Services;
using WillyShop.Models;

namespace WillyShop.Controllers
{
    [AllowAnonymous]
    [Route("api/[controller]")]
    public class ApiAccountController : Controller
    {
        private readonly SignInManager<WillyShop.Models.ApplicationUser> _signInManager;
        private readonly UserManager<WillyShop.Models.ApplicationUser> _userManager;
        private readonly CartService _cartService;

        public ApiAccountController(SignInManager<WillyShop.Models.ApplicationUser> signInManager, UserManager<WillyShop.Models.ApplicationUser> userManager, CartService cartService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _cartService = cartService;
        }

        public class LoginRequest
        {
            public string Account { get; set; } = null!;
            public string Password { get; set; } = null!;
            public bool RememberMe { get; set; }
            public string? ReturnUrl { get; set; }
        }

        public class LoginResponse
        {
            public bool Succeeded { get; set; }
            public string? ReturnUrl { get; set; }
            public string? Error { get; set; }
        }

        // 中文註解：AJAX 登入端點，接受 Email/Password，登入成功後合併匿名購物車並回傳結果。
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            if (string.IsNullOrEmpty(req?.Account) || string.IsNullOrEmpty(req.Password))
            {
                return BadRequest(new LoginResponse { Succeeded = false, Error = "請輸入帳號與密碼。" });
            }

            var result = await _signInManager.PasswordSignInAsync(req.Account, req.Password, req.RememberMe, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                var user = await _userManager.FindByNameAsync(req.Account);
                if (user != null)
                {
                    // 合併匿名購物車
                    var anonJson = HttpContext.Session.GetString("AnonCart");
                    if (!string.IsNullOrEmpty(anonJson))
                    {
                        try
                        {
                            var anonItems = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<CartItem>>(anonJson) ?? new System.Collections.Generic.List<CartItem>();
                            await _cartService.MergeAnonymousCartToUserAsync(user.Id, anonItems);
                            HttpContext.Session.Remove("AnonCart");
                        }
                        catch
                        {
                            // 忽略合併錯誤，仍視為登入成功
                        }
                    }
                }

                return Ok(new LoginResponse { Succeeded = true, ReturnUrl = req.ReturnUrl });
            }

            return Unauthorized(new LoginResponse { Succeeded = false, Error = "登入失敗，帳號或密碼錯誤。" });
        }
    }
}
