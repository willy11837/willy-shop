namespace WillyShop.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<WillyShopWebApplicationFactory>
{
    public const string Name = "WillyShop SQL Server integration tests";
}