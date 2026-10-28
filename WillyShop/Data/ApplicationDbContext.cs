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

        public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;

        public DbSet<BackInStockSubscription> BackInStockSubscriptions { get; set; } = null!;

        public DbSet<UserNotification> UserNotifications { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<RefreshToken>(entity =>
            {
                entity.HasIndex(token => token.TokenHash).IsUnique();
                entity.HasIndex(token => token.UserId);
                entity.HasIndex(token => token.TokenFamily);
                entity.HasIndex(token => token.ExpiresAt);
                entity.HasOne(token => token.User)
                    .WithMany()
                    .HasForeignKey(token => token.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne<RefreshToken>()
                    .WithMany()
                    .HasForeignKey(token => token.ReplacedByTokenId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<BackInStockSubscription>(entity =>
            {
                entity.HasIndex(value => new { value.UserId, value.ProductCode })
                    .IsUnique()
                    .HasFilter("[NotifiedAt] IS NULL");
                entity.HasIndex(value => new { value.ProductCode, value.NotifiedAt });
                entity.HasOne(value => value.User)
                    .WithMany()
                    .HasForeignKey(value => value.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(value => value.Product)
                    .WithMany()
                    .HasForeignKey(value => value.ProductCode)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<UserNotification>(entity =>
            {
                entity.HasIndex(value => new { value.UserId, value.CreatedAt });
                entity.HasIndex(value => new { value.UserId, value.ReadAt });
                entity.HasOne(value => value.User)
                    .WithMany()
                    .HasForeignKey(value => value.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
