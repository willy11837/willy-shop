using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using WillyShop.Data;
using WillyShop.Models;

namespace WillyShop.Pages.Admin.Products
{
    [Authorize(Roles = SystemRoles.ProductManagers)]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public IndexModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public List<Product> Products { get; set; } = new List<Product>();

        // 中文註解：管理員用的商品列表，受 Role=Admin 保護。
        public async Task OnGetAsync()
        {
            Products = await _db.Products.ToListAsync();
        }
    }
}
