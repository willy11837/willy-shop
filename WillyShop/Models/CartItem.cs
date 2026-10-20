using System;

namespace WillyShop.Models
{
    /// <summary>
    /// 匿名購物車項目（用於 Session 序列化）
    /// </summary>
    public class CartItem
    {
        public string ProductCode { get; set; } = null!;
        public int Quantity { get; set; }
    }
}
