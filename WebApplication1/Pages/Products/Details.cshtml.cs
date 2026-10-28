using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication1.Data;
using WebApplication1.Models;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Services;
using System.Security.Claims;

namespace WebApplication1.Pages.Products
{
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly CartService _cartService;

        public DetailsModel(ApplicationDbContext db, CartService cartService)
        {
            _db = db;
            _cartService = cartService;
        }

        public Product? Product { get; set; }

        public async Task<IActionResult> OnGetAsync(string? code)
        {
            if (string.IsNullOrEmpty(code)) return NotFound();

            Product = await _db.Products.FindAsync(code);
            if (Product == null) return NotFound();

            return Page();
        }

        // 中文註解：處理從商品細節頁發出的加入購物車請求。
        // 未登入時將商品加入匿名購物車（Session）；已登入時將商品存入資料庫的使用者購物車。
        // 參數：string? code - 商品代碼；int qty - 加入數量。
        public async Task<IActionResult> OnPostAddToCartAsync(string? code, int qty = 1)
        {
            if (string.IsNullOrEmpty(code)) return BadRequest();

            var product = await _db.Products.FindAsync(code);
            if (product == null) return NotFound();

            var item = new CartItem { ProductCode = code, Quantity = Math.Max(1, qty) };

            if (User?.Identity?.IsAuthenticated == true)
            {
                // 已登入：取得使用者 Id 並直接合併到使用者購物車
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userId))
                {
                    await _cartService.MergeAnonymousCartToUserAsync(userId, new List<CartItem> { item });
                    return RedirectToPage("/Cart/Index");
                }
            }

            // 未登入：加入匿名購物車（Session）
            await _cartService.AddToAnonymousCartAsync(HttpContext, item);
            // 如果是 AJAX 請求，回傳 JSON；否則 redirect 回頁面
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return new JsonResult(new { success = true });
            }
            return RedirectToPage();
        }
    }
}