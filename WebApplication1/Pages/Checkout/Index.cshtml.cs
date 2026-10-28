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

namespace WebApplication1.Pages.Checkout
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
        public decimal Total { get; set; }

        // 中文註解：顯示使用者的購物車明細以供結帳，包含計算總金額。
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

            Total = Items.Sum(i => i.Subtotal);
        }

        // 中文註解：執行結帳流程：建立 Order 與 OrderItem（包含價格快照），在成功建立後清空使用者購物車，並以交易包裝以確保原子性。
        public async Task<IActionResult> OnPostCheckoutAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return RedirectToPage();

            // 取得購物車項目
            var userCart = await _db.UserCartItems.Where(x => x.UserId == userId).ToListAsync();
            if (userCart.Count == 0) return RedirectToPage();

            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                // 計算總金額並建立 Order
                var productCodes = userCart.Select(x => x.ProductCode).Distinct().ToList();
                var products = await _db.Products.Where(p => productCodes.Contains(p.ProductCode)).ToDictionaryAsync(p => p.ProductCode);

                decimal total = 0m;
                foreach (var c in userCart)
                {
                    var price = products.ContainsKey(c.ProductCode) ? products[c.ProductCode].Price : 0m;
                    total += price * c.Quantity;
                }

                var order = new WebApplication1.Models.Order
                {
                    UserId = userId,
                    TotalAmount = total,
                    CreatedAt = DateTime.UtcNow,
                    Status = "Pending"
                };
                await _db.Orders.AddAsync(order);
                await _db.SaveChangesAsync();

                // 建立 OrderItems
                foreach (var c in userCart)
                {
                    var prod = products.ContainsKey(c.ProductCode) ? products[c.ProductCode] : null;
                    var unit = prod != null ? prod.Price : 0m;
                    var oi = new WebApplication1.Models.OrderItem
                    {
                        OrderId = order.Id,
                        ProductCode = c.ProductCode,
                        ProductName = prod != null ? prod.Name : c.ProductCode,
                        UnitPrice = unit,
                        Quantity = c.Quantity,
                        Subtotal = unit * c.Quantity
                    };
                    await _db.OrderItems.AddAsync(oi);
                }

                // 清空使用者購物車
                _db.UserCartItems.RemoveRange(userCart);
                await _db.SaveChangesAsync();

                await transaction.CommitAsync();

                // 導向結帳完成頁，傳遞 Order Id
                return RedirectToPage("/Checkout/Complete", new { orderId = order.Id });
            }
            catch
            {
                await transaction.RollbackAsync();
                // 發生錯誤時可記錄或顯示錯誤訊息
                return RedirectToPage();
            }
        }
    }
}
