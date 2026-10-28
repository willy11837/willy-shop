using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Threading.Tasks;
using WebApplication1.Data;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;
using System.Text;
using System.Linq;
using System.Globalization;

namespace WebApplication1.Pages.Checkout
{
    public class CompleteModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public CompleteModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public Order? Order { get; set; }
        public System.Collections.Generic.List<OrderItem> Items { get; set; } = new System.Collections.Generic.List<OrderItem>();

        // 中文註解：顯示剛建立的訂單摘要。
        // 中文註解：載入指定訂單與其明細以顯示訂單摘要與項目列表。
        public async Task<IActionResult> OnGetAsync(int orderId)
        {
            Order = await _db.Orders.FindAsync(orderId);
            if (Order == null) return NotFound();

            Items = await _db.OrderItems.Where(o => o.OrderId == orderId).ToListAsync();
            return Page();
        }

        // 中文註解：匯出指定訂單為 CSV 檔案，包含商品代碼、名稱、單價、數量與小計。
        // 回傳：CSV 檔案下載（text/csv）。
        public async Task<IActionResult> OnGetExportCsvAsync(int orderId)
        {
            var order = await _db.Orders.FindAsync(orderId);
            if (order == null) return NotFound();

            var items = await _db.OrderItems.Where(o => o.OrderId == orderId).ToListAsync();

            var sb = new StringBuilder();
            // CSV 標頭
            sb.AppendLine("ProductCode,ProductName,UnitPrice,Quantity,Subtotal");
            foreach (var it in items)
            {
                // 以逗號分隔，簡單處理雙引號
                string name = it.ProductName?.Replace("\"", "\"\"") ?? "";
                sb.Append('"').Append(name).Append('"').Append(',');
                sb.Append(it.ProductCode).Append(',');
                sb.Append(it.UnitPrice.ToString("F2", CultureInfo.InvariantCulture)).Append(',');
                sb.Append(it.Quantity).Append(',');
                sb.Append(it.Subtotal.ToString("F2", CultureInfo.InvariantCulture)).AppendLine();
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var filename = $"order_{orderId}.csv";
            return File(bytes, "text/csv; charset=utf-8", filename);
        }
    }
}
