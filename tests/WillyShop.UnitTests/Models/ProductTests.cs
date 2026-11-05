using WillyShop.Models;

namespace WillyShop.UnitTests.Models;

public sealed class ProductTests
{
    [Theory]
    [InlineData(false, 150, 120, 150)]
    [InlineData(true, 150, null, 150)]
    [InlineData(true, 150, 120, 120)]
    public void EffectivePrice_不同特價設定_回傳適用價格(
        bool isOnSale,
        int price,
        int? salePrice,
        int expected)
    {
        // Arrange
        var product = new Product
        {
            ProductCode = "P001",
            Name = "測試商品",
            Price = price,
            IsOnSale = isOnSale,
            SalePrice = salePrice
        };

        // Act
        var actual = product.EffectivePrice;

        // Assert
        Assert.Equal((decimal)expected, actual);
    }
}