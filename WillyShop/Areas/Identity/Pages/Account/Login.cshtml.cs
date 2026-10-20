using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using WillyShop.Services;
using WillyShop.Models;

namespace WillyShop.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly SignInManager<WillyShop.Models.ApplicationUser> _signInManager;
        private readonly UserManager<WillyShop.Models.ApplicationUser> _userManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly CartService _cartService;

        public LoginModel(SignInManager<WillyShop.Models.ApplicationUser> signInManager,
            UserManager<WillyShop.Models.ApplicationUser> userManager,
            ILogger<LoginModel> logger,
            CartService cartService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _cartService = cartService;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        public string? ReturnUrl { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "請輸入使用者帳號")]
            public string Account { get; set; } = null!;

            [Required(ErrorMessage = "請輸入密碼")]
            [DataType(DataType.Password)]
            public string Password { get; set; } = null!;

            public bool RememberMe { get; set; }
        }

        // 中文註解：處理登入 POST 請求，登入成功後若 Session 中有匿名購物車則合併到資料庫的使用者購物車，再清除 Session，最後導回 ReturnUrl。
        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var account = Input.Account.Trim();
            var result = await _signInManager.PasswordSignInAsync(account, Input.Password, Input.RememberMe, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                _logger.LogInformation("User logged in.");

                // 取得已登入的使用者（可能以 Email 作為使用者名稱或以 Email 搜尋）
                var user = await _userManager.FindByNameAsync(account);
                if (user != null)
                {
                    var anonJson = HttpContext.Session.GetString("AnonCart");
                    if (!string.IsNullOrEmpty(anonJson))
                    {
                        try
                        {
                            var anonItems = JsonSerializer.Deserialize<List<CartItem>>(anonJson) ?? new List<CartItem>();
                            await _cartService.MergeAnonymousCartToUserAsync(user.Id, anonItems);
                            HttpContext.Session.Remove("AnonCart");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to merge anonymous cart after login.");
                        }
                    }
                }

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return LocalRedirect(returnUrl);
                }
                else
                {
                    return RedirectToPage("/Products/Index");
                }
            }

            ModelState.AddModelError(string.Empty, result.IsLockedOut
                ? "帳號已暫時鎖定，請稍後再試。"
                : "登入失敗，帳號或密碼錯誤。");
            return Page();
        }
    }
}
