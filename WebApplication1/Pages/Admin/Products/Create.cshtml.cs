using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Pages.Admin.Products
{
    [Authorize(Roles = SystemRoles.ProductManagers)]
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        public CreateModel(ApplicationDbContext db) => _db = db;

        [BindProperty]
        public Product Product { get; set; } = new();

        public async Task<IActionResult> OnPostAsync()
        {
            Product.ProductCode = Product.ProductCode?.Trim() ?? string.Empty;
            Product.Name = Product.Name?.Trim() ?? string.Empty;
            if (await _db.Products.FindAsync(Product.ProductCode) != null)
                ModelState.AddModelError("Product.ProductCode", "商品代碼已存在。");
            if (!ModelState.IsValid) return Page();

            Product.CreatedAt = DateTime.UtcNow;
            Product.ModifiedAt = DateTime.UtcNow;
            Product.ModifiedBy = User.Identity?.Name;
            _db.Products.Add(Product);
            await _db.SaveChangesAsync();
            return RedirectToPage("./Index");
        }
    }
}