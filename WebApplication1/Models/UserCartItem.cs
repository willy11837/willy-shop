using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models
{
    /// <summary>
    /// 使用者購物車項目（儲存在資料庫中，屬於特定使用者）
    /// </summary>
    public class UserCartItem
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = null!;

        [Required]
        public string ProductCode { get; set; } = null!;

        [Required]
        public int Quantity { get; set; }
    }
}
