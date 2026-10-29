using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WillyShop.Models
{
    public static class OrderStatuses
    {
        public const string Pending = "Pending";
        public const string Shipped = "Shipped";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";
        public const string ReturnRequested = "ReturnRequested";
        public const string ReturnApproved = "ReturnApproved";
        public const string Refunded = "Refunded";

        public static readonly string[] All =
        [
            Pending, Shipped, Completed, Cancelled, ReturnRequested, ReturnApproved, Refunded
        ];

        public static string DisplayName(string status) => status switch
        {
            Pending => "待處理",
            Shipped => "寄送中",
            Completed => "已完成",
            Cancelled => "已取消",
            ReturnRequested => "退貨申請中",
            ReturnApproved => "退貨已核准",
            Refunded => "已退款",
            _ => status
        };
    }

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

        [Required, StringLength(30)]
        public string Status { get; set; } = OrderStatuses.Pending;

        public DateTime? ModifiedAt { get; set; }
        public DateTime? ShippedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public DateTime? ReturnRequestedAt { get; set; }

        [StringLength(500)]
        public string? ReturnReason { get; set; }

        public DateTime? ReturnReviewedAt { get; set; }

        [StringLength(450)]
        public string? ReturnReviewedBy { get; set; }

        [StringLength(500)]
        public string? ReturnReviewNote { get; set; }

        public DateTime? RefundCompletedAt { get; set; }

        [StringLength(450)]
        public string? RefundProcessedBy { get; set; }

        [StringLength(500)]
        public string? RefundNote { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? RefundAmount { get; set; }

        public DateTime? InventoryRestockedAt { get; set; }
    }
}
