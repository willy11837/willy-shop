using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace WillyShop.Models
{
    public static class SystemRoles
    {
        public const string System = "System";
        public const string Admin = "Admin";
        public const string Store = "Store";
        public const string Normal = "Normal";
        public const string Guest = "Guest";

        public static readonly string[] All = [System, Admin, Store, Normal, Guest];
        public const string ProductManagers = Store + "," + Admin + "," + System;
        public const string AccountManagers = Admin + "," + System;
        public const string OrderManagers = Store + "," + Admin + "," + System;
    }

    public class ApplicationUser : IdentityUser
    {
        [Display(Name = "使用者名稱")]
        [StringLength(50)]
        public string? DisplayName { get; set; }

        [Display(Name = "新增時間")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Display(Name = "最後修改人員")]
        public string? ModifiedBy { get; set; }

        [Display(Name = "最後修改時間")]
        public DateTime? ModifiedAt { get; set; }
    }
}
