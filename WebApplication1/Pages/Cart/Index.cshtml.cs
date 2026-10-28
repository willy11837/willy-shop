using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Pages.Cart
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public IndexModel(ApplicationDbContext db)
        {
            _db = db;
        }

        // 顯示用的購物車項目檢視模型（包含商品名稱、單價、小計）
        public class CartViewItem
        {
            public int Id { get; set; }
            public string ProductCode { get; set; } = null!;
            public string Name { get; set; } = null!;
            public decimal UnitPrice { get; set; }
            public int Quantity { get; set; }
            public decimal Subtotal { get; set; }
        }

        public List<CartViewItem> Items { get; set; } = new List<CartViewItem>();

        // 中文註解：讀取目前登入使用者的購物車項目，並從 Products 表查詢商品明細以填充顯示用的 CartViewItem，計算每項小計與總計。
        public async Task OnGetAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return;

            var userCart = await _db.UserCartItems.Where(x => x.UserId == userId).ToListAsync();
            var productCodes = userCart.Select(x => x.ProductCode).Distinct().ToList();
            var products = await _db.Products.Where(p => productCodes.Contains(p.ProductCode)).ToDictionaryAsync(p => p.ProductCode);

            Items = userCart.Select(c => new CartViewItem
            {
                Id = c.Id,
                ProductCode = c.ProductCode,
                Name = products.ContainsKey(c.ProductCode) ? products[c.ProductCode].Name : c.ProductCode,
                UnitPrice = products.ContainsKey(c.ProductCode) ? products[c.ProductCode].Price : 0m,
                Quantity = c.Quantity,
                Subtotal = (products.ContainsKey(c.ProductCode) ? products[c.ProductCode].Price : 0m) * c.Quantity
            }).ToList();
        }

        // 中文註解：從購物車移除指定項目（資料庫中的 UserCartItem）。
        public async Task<IActionResult> OnPostRemoveAsync(int id)
        {
            var item = await _db.UserCartItems.FindAsync(id);
            if (item != null)
            {
                _db.UserCartItems.Remove(item);
                await _db.SaveChangesAsync();
            }
            return RedirectToPage();
        }
    }
}
