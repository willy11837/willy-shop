using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WillyShop.Models
{
    /// <summary>
    /// 訂單項目（OrderItem）：紀錄訂單內每一個商品的快照（名稱、單價、數量、小計）。
    /// </summary>
    public class OrderItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int OrderId { get; set; } // 對應 Order.Id

        [Required]
        public string ProductCode { get; set; } = null!;

        [Required]
        public string ProductName { get; set; } = null!; // 當時商品名稱快照

        // 中文註解：下單時的單價（兩位小數）
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [Required]
        public int Quantity { get; set; }

        // 中文註解：單項小計 = UnitPrice * Quantity
        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }
    }
}
