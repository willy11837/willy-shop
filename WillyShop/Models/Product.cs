using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WillyShop.Models
{
    public enum ProductStatus
    {
        可銷售 = 0,
        凍結 = 1,
        數量不足 = 2
    }

    public class Product
    {
        [Key]
        [Required]
        public string ProductCode { get; set; } = null!; // 商品代碼 (PK)

        [Required]
        public string Name { get; set; } = null!; // 品名

        // 中文註解：商品單價（單位：新台幣，保留兩位小數）
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; } = 0m; // 單價

        public string? Category { get; set; } // 商品類別

        public int StockQuantity { get; set; } // 庫存數量

        public ProductStatus Status { get; set; } // 商品狀態

        public string? ImagePath { get; set; } // 商品圖片路徑

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // 新增時間 (UTC)

        public string? ModifiedBy { get; set; } // 最後修改人員

        public DateTime? ModifiedAt { get; set; } // 最後修改時間
    }
}
