using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Pages.Admin.Products
{
    [Authorize(Roles = SystemRoles.ProductManagers)]
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        public EditModel(ApplicationDbContext db) => _db = db;

        [BindProperty]
        public Product Product { get; set; } = null!;

        public async Task<IActionResult> OnGetAsync(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return NotFound();
            var product = await _db.Products.FindAsync(code);
            if (product == null) return NotFound();
            Product = product;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();
            var product = await _db.Products.FindAsync(Product.ProductCode);
            if (product == null) return NotFound();
            product.Name = Product.Name.Trim();
            product.Category = Product.Category;
            product.Price = Product.Price;
            product.StockQuantity = Product.StockQuantity;
            product.Status = Product.Status;
            product.ImagePath = Product.ImagePath;
            product.ModifiedBy = User.Identity?.Name;
            product.ModifiedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return RedirectToPage("/Products/Details", new { code = product.ProductCode });
        }
    }
}