using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WillyShop.Data;

namespace WillyShop.IntegrationTests.Infrastructure;

public sealed class WillyShopWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string TestDatabasePrefix = "WillyShopTests_";
    private static readonly string SigningKey = "willy-shop-integration-tests-only-signing-key-2026";
    private readonly string _databaseName = $"{TestDatabasePrefix}{Guid.NewGuid():N}";

    public string ConnectionString =>
        $"Server=(localdb)\\mssqllocaldb;Database={_databaseName};Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        AssertSafeTestDatabase();
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
        builder.UseSetting("Jwt:Issuer", "WillyShop.IntegrationTests");
        builder.UseSetting("Jwt:Audience", "WillyShop.IntegrationTests.Client");
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        builder.UseSetting("SeedUsers:SystemPassword", string.Empty);
        builder.UseSetting("SeedUsers:AdminPassword", string.Empty);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString,
                ["Jwt:Issuer"] = "WillyShop.IntegrationTests",
                ["Jwt:Audience"] = "WillyShop.IntegrationTests.Client",
                ["Jwt:SigningKey"] = SigningKey,
                ["SeedUsers:SystemPassword"] = string.Empty,
                ["SeedUsers:AdminPassword"] = string.Empty
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(ConnectionString));
        });
    }

    public async ValueTask InitializeAsync()
    {
        _ = Services;
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
    }

    public new async ValueTask DisposeAsync()
    {
        AssertSafeTestDatabase();
        await using (var db = CreateDbContext())
        {
            await db.Database.EnsureDeletedAsync();
        }
        await base.DisposeAsync();
    }

    public ApplicationDbContext CreateDbContext()
    {
        AssertSafeTestDatabase();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new ApplicationDbContext(options);
    }

    private void AssertSafeTestDatabase()
    {
        if (!_databaseName.StartsWith(TestDatabasePrefix, StringComparison.Ordinal)
            || string.Equals(_databaseName, "WebApp1Db", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("整合測試拒絕使用非獨立測試資料庫。");
        }
    }
}