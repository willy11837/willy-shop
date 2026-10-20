using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using WillyShop.Models;

namespace WillyShop.Data
{
    /// <summary>
    /// 應用程式的資料庫上下文。
    /// 繼承自 IdentityDbContext，使此 DbContext 可以作為 ASP.NET Core Identity 的儲存層。
    /// </summary>
    public class ApplicationDbContext : IdentityDbContext<WillyShop.Models.ApplicationUser>
    {
        /// <summary>
        /// 建構子：注入 DbContext 選項。
        /// </summary>
        /// <param name="options">DbContext 的設定選項。</param>
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Product> Products { get; set; } = null!;

        public DbSet<SystemUser> SystemUsers { get; set; } = null!;

        /// <summary>
        /// 資料庫中的使用者購物車項目集合（對應 UserCartItem 實體）。
        /// </summary>
        public DbSet<WillyShop.Models.UserCartItem> UserCartItems { get; set; } = null!;

        /// <summary>
        /// 資料庫中的訂單（Order）集合，用於儲存使用者已建立的訂單。
        /// </summary>
        public DbSet<WillyShop.Models.Order> Orders { get; set; } = null!;

        /// <summary>
        /// 資料庫中的訂單項目（OrderItem）集合，對應每筆訂單的明細。
        /// </summary>
        public DbSet<WillyShop.Models.OrderItem> OrderItems { get; set; } = null!;
    }
}
