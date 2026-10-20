using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WillyShop.Models
{
    /// <summary>
    /// 訂單實體（Order）：代表使用者已建立的訂單。
    /// </summary>
    public class Order
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = null!; // 建立此訂單的使用者 Id

        // 中文註解：訂單總金額（使用 decimal 儲存，兩位小數）
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? Status { get; set; } = "Pending"; // 訂單狀態（例如 Pending, Paid, Shipped）
    }
}
