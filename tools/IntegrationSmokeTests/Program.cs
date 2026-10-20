using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WillyShop.Data;
using WillyShop.Models;

var baseUrl = Environment.GetEnvironmentVariable("SMOKE_BASE_URL") ?? "http://localhost:5151";
const string AdminAccount = "admin";
var adminPassword = Environment.GetEnvironmentVariable("SMOKE_ADMIN_PASSWORD");
if (string.IsNullOrWhiteSpace(adminPassword))
{
    throw new InvalidOperationException("SMOKE_ADMIN_PASSWORD must be set before running integration smoke tests.");
}
var systemPassword = Environment.GetEnvironmentVariable("SMOKE_SYSTEM_PASSWORD");
if (string.IsNullOrWhiteSpace(systemPassword))
{
    throw new InvalidOperationException("SMOKE_SYSTEM_PASSWORD must be set before running integration smoke tests.");
}
var suffix = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
var productCode = $"IT{suffix[^10..]}";
var account = $"it{suffix[^12..]}";
var email = $"{account}@example.test";
var password = "TestUser@123";
var results = new List<(string Name, bool Passed, string Detail)>();
int? orderId = null;
string? userId = null;

var connectionString = "Server=(localdb)\\mssqllocaldb;Database=WebApp1Db;Trusted_Connection=True;MultipleActiveResultSets=true";
var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connectionString).Options;

try
{
    await WaitForServerAsync();
    await RunAsync("公開商品 API", async () =>
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/api/v1/products");
        Ensure(response.StatusCode == HttpStatusCode.OK, $"HTTP {(int)response.StatusCode}");
        var products = await ReadJsonAsync<JsonElement>(response);
        Ensure(products.ValueKind == JsonValueKind.Array, "回應不是陣列");
    });

    await RunAsync("匿名狀態與訂單 401", async () =>
    {
        using var client = CreateClient();
        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        Ensure(!me.GetProperty("isAuthenticated").GetBoolean(), "匿名狀態錯誤");
        var orders = await client.GetAsync("/api/v1/orders");
        Ensure(orders.StatusCode == HttpStatusCode.Unauthorized, $"預期 401，實際 {(int)orders.StatusCode}");
    });

    using var admin = CreateClient();
    await LoginAsync(admin, AdminAccount, adminPassword);
    await RunAsync("Admin Cookie 登入與角色", async () =>
    {
        var me = await admin.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        Ensure(me.GetProperty("isAuthenticated").GetBoolean(), "未登入");
        Ensure(me.GetProperty("roles").EnumerateArray().Any(role => role.GetString() == "Admin"), "缺少 Admin 角色");
    });

    await RunAsync("管理商品新增與 DB 寫入", async () =>
    {
        var response = await SendWithCsrfAsync(admin, HttpMethod.Post, "/api/v1/admin/products", new
        {
            productCode,
            name = "整合測試商品",
            price = 125.50m,
            category = "自動測試",
            stockQuantity = 10,
            status = 0,
            imagePath = "~/images/product-placeholder.svg"
        });
        Ensure(response.StatusCode == HttpStatusCode.Created, $"HTTP {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        await using var db = new ApplicationDbContext(dbOptions);
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(value => value.ProductCode == productCode);
        Ensure(product is not null && product.Price == 125.50m && product.StockQuantity == 10, "DB 商品資料不正確");
    });

    await RunAsync("管理商品修改與 DB 更新", async () =>
    {
        var response = await SendWithCsrfAsync(admin, HttpMethod.Put, $"/api/v1/admin/products/{productCode}", new
        {
            productCode,
            name = "整合測試商品-已修改",
            price = 150m,
            category = "自動測試",
            stockQuantity = 12,
            status = 0,
            imagePath = "~/images/product-placeholder.svg"
        });
        Ensure(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}");
        await using var db = new ApplicationDbContext(dbOptions);
        var product = await db.Products.AsNoTracking().SingleAsync(value => value.ProductCode == productCode);
        Ensure(product.Name.EndsWith("已修改") && product.Price == 150m && product.StockQuantity == 12, "DB 商品修改未生效");
    });

    using var shopper = CreateClient();
    await RunAsync("匿名購物車 Session 新增與更新", async () =>
    {
        var add = await SendWithCsrfAsync(shopper, HttpMethod.Post, $"/api/v1/cart/items/{productCode}", new { quantity = 2 });
        Ensure(add.IsSuccessStatusCode, $"新增 HTTP {(int)add.StatusCode}");
        var added = await ReadJsonAsync<JsonElement>(add);
        Ensure(added.GetProperty("totalQuantity").GetInt32() == 2, "匿名購物車新增數量錯誤");
        var update = await SendWithCsrfAsync(shopper, HttpMethod.Put, $"/api/v1/cart/items/{productCode}", new { quantity = 3 });
        var updated = await ReadJsonAsync<JsonElement>(update);
        Ensure(updated.GetProperty("totalQuantity").GetInt32() == 3, "匿名購物車更新數量錯誤");
    });

    await RunAsync("註冊、Normal 角色與匿名購物車合併", async () =>
    {
        var response = await SendWithCsrfAsync(shopper, HttpMethod.Post, "/api/v1/auth/register", new
        {
            account,
            displayName = "整合測試會員",
            email,
            password,
            confirmPassword = password
        });
        Ensure(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var registered = await ReadJsonAsync<JsonElement>(response);
        userId = registered.GetProperty("id").GetString();
        Ensure(registered.GetProperty("roles").EnumerateArray().Any(role => role.GetString() == "Normal"), "缺少 Normal 角色");
        var cart = await shopper.GetFromJsonAsync<JsonElement>("/api/v1/cart");
        Ensure(cart.GetProperty("totalQuantity").GetInt32() == 3, "購物車未合併");
        await using var db = new ApplicationDbContext(dbOptions);
        var dbCart = await db.UserCartItems.AsNoTracking().SingleOrDefaultAsync(item => item.UserId == userId && item.ProductCode == productCode);
        Ensure(dbCart?.Quantity == 3, "DB 會員購物車數量錯誤");
    });

    await RunAsync("Normal 使用者禁止管理 API", async () =>
    {
        var response = await shopper.GetAsync("/api/v1/admin/products");
        Ensure(response.StatusCode == HttpStatusCode.Forbidden, $"預期 403，實際 {(int)response.StatusCode}");
    });

    await RunAsync("會員購物車刪除與重新加入", async () =>
    {
        var remove = await SendWithCsrfAsync(shopper, HttpMethod.Delete, $"/api/v1/cart/items/{productCode}", null);
        var removed = await ReadJsonAsync<JsonElement>(remove);
        Ensure(removed.GetProperty("totalQuantity").GetInt32() == 0, "移除失敗");
        var add = await SendWithCsrfAsync(shopper, HttpMethod.Post, $"/api/v1/cart/items/{productCode}", new { quantity = 4 });
        var added = await ReadJsonAsync<JsonElement>(add);
        Ensure(added.GetProperty("totalAmount").GetDecimal() == 600m, "會員購物車金額錯誤");
    });

    await RunAsync("結帳、訂單明細、庫存扣減與購物車清空", async () =>
    {
        var response = await SendWithCsrfAsync(shopper, HttpMethod.Post, "/api/v1/orders/checkout", null);
        Ensure(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        var order = await ReadJsonAsync<JsonElement>(response);
        orderId = order.GetProperty("id").GetInt32();
        Ensure(order.GetProperty("totalAmount").GetDecimal() == 600m, "訂單總額錯誤");
        Ensure(order.GetProperty("items")[0].GetProperty("quantity").GetInt32() == 4, "訂單明細數量錯誤");
        await using var db = new ApplicationDbContext(dbOptions);
        var dbOrder = await db.Orders.AsNoTracking().SingleOrDefaultAsync(value => value.Id == orderId && value.UserId == userId);
        var dbItem = await db.OrderItems.AsNoTracking().SingleOrDefaultAsync(value => value.OrderId == orderId && value.ProductCode == productCode);
        var product = await db.Products.AsNoTracking().SingleAsync(value => value.ProductCode == productCode);
        var cartCount = await db.UserCartItems.CountAsync(value => value.UserId == userId);
        Ensure(dbOrder?.TotalAmount == 600m, "DB 訂單錯誤");
        Ensure(dbItem?.Quantity == 4 && dbItem.Subtotal == 600m, "DB 訂單明細錯誤");
        Ensure(product.StockQuantity == 8, $"庫存應為 8，實際 {product.StockQuantity}");
        Ensure(cartCount == 0, "結帳後 DB 購物車未清空");
    });

    await RunAsync("訂單列表與單筆權限", async () =>
    {
        var orders = await shopper.GetFromJsonAsync<JsonElement>("/api/v1/orders");
        Ensure(orders.EnumerateArray().Any(order => order.GetProperty("id").GetInt32() == orderId), "訂單列表找不到新訂單");
        var detail = await shopper.GetAsync($"/api/v1/orders/{orderId}");
        Ensure(detail.IsSuccessStatusCode, "本人無法取得訂單");
        var adminDetail = await admin.GetAsync($"/api/v1/orders/{orderId}");
        Ensure(adminDetail.IsSuccessStatusCode, "Admin 無法取得訂單");
    });

    await RunAsync("Admin 帳號管理新增、修改", async () =>
    {
        var managedAccount = $"m{account}";
        var managedEmail = $"m{email}";
        var create = await SendWithCsrfAsync(admin, HttpMethod.Post, "/api/v1/admin/users", new
        {
            account = managedAccount,
            displayName = "受管理測試帳號",
            email = managedEmail,
            password,
            role = "Guest"
        });
        Ensure(create.IsSuccessStatusCode, $"新增 HTTP {(int)create.StatusCode}: {await create.Content.ReadAsStringAsync()}");
        var managed = await ReadJsonAsync<JsonElement>(create);
        var managedId = managed.GetProperty("id").GetString()!;
        var update = await SendWithCsrfAsync(admin, HttpMethod.Put, $"/api/v1/admin/users/{managedId}", new
        {
            displayName = "受管理測試帳號-已修改",
            email = managedEmail,
            newPassword = "Changed@123",
            role = "Normal"
        });
        Ensure(update.IsSuccessStatusCode, $"修改 HTTP {(int)update.StatusCode}");
        await using var db = new ApplicationDbContext(dbOptions);
        var dbUser = await db.Users.AsNoTracking().SingleAsync(user => user.Id == managedId);
        var normalRoleId = await db.Roles.Where(role => role.Name == "Normal").Select(role => role.Id).SingleAsync();
        Ensure(dbUser.DisplayName?.EndsWith("已修改") == true, "DB 帳號修改未生效");
        Ensure(await db.UserRoles.AnyAsync(value => value.UserId == managedId && value.RoleId == normalRoleId), "DB 角色修改未生效");
    });

    await RunAsync("帳號刪除角色限制與 System 刪除", async () =>
    {
        await using var lookup = new ApplicationDbContext(dbOptions);
        var managedId = await lookup.Users.Where(user => user.UserName == $"m{account}").Select(user => user.Id).SingleAsync();
        var adminDelete = await SendWithCsrfAsync(admin, HttpMethod.Delete, $"/api/v1/admin/users/{managedId}", null);
        Ensure(adminDelete.StatusCode == HttpStatusCode.Forbidden, $"Admin 刪除預期 403，實際 {(int)adminDelete.StatusCode}");

        using var system = CreateClient();
        await LoginAsync(system, "system", systemPassword);
        var systemDelete = await SendWithCsrfAsync(system, HttpMethod.Delete, $"/api/v1/admin/users/{managedId}", null);
        Ensure(systemDelete.StatusCode == HttpStatusCode.NoContent, $"System 刪除 HTTP {(int)systemDelete.StatusCode}");
        await using var verify = new ApplicationDbContext(dbOptions);
        Ensure(!await verify.Users.AnyAsync(user => user.Id == managedId), "DB 帳號未刪除");
    });

    await RunAsync("商品刪除同步清除購物車參照", async () =>
    {
        await using (var db = new ApplicationDbContext(dbOptions))
        {
            db.UserCartItems.Add(new UserCartItem { UserId = userId!, ProductCode = productCode, Quantity = 1 });
            await db.SaveChangesAsync();
        }
        var delete = await SendWithCsrfAsync(admin, HttpMethod.Delete, $"/api/v1/admin/products/{productCode}", null);
        Ensure(delete.StatusCode == HttpStatusCode.NoContent, $"HTTP {(int)delete.StatusCode}");
        await using var verify = new ApplicationDbContext(dbOptions);
        Ensure(!await verify.Products.AnyAsync(value => value.ProductCode == productCode), "商品未刪除");
        Ensure(!await verify.UserCartItems.AnyAsync(value => value.ProductCode == productCode), "購物車參照未清除");
    });

    await RunAsync("登出與 Cookie 清除", async () =>
    {
        var response = await SendWithCsrfAsync(shopper, HttpMethod.Post, "/api/v1/auth/logout", null);
        Ensure(response.StatusCode == HttpStatusCode.NoContent, $"HTTP {(int)response.StatusCode}");
        var me = await shopper.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        Ensure(!me.GetProperty("isAuthenticated").GetBoolean(), "登出後仍為登入狀態");
    });
}
finally
{
    try
    {
        await CleanupAsync();
        await using var verifyCleanup = new ApplicationDbContext(dbOptions);
        var hasUsers = await verifyCleanup.Users.AnyAsync(user => user.UserName == account || user.UserName == $"m{account}");
        var hasProduct = await verifyCleanup.Products.AnyAsync(product => product.ProductCode == productCode);
        results.Add(("測試資料清理與 DB 無殘留", !hasUsers && !hasProduct, hasUsers || hasProduct ? "仍有測試資料殘留" : "PASS"));
    }
    catch (Exception exception)
    {
        results.Add(("測試資料清理與 DB 無殘留", false, exception.Message));
    }
    PrintResults();
}

return results.All(result => result.Passed) ? 0 : 1;

HttpClient CreateClient()
{
    var handler = new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false };
    return new HttpClient(handler) { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(20) };
}

async Task LoginAsync(HttpClient client, string loginAccount, string loginPassword)
{
    var response = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/auth/login", new { account = loginAccount, password = loginPassword, rememberMe = false });
    Ensure(response.IsSuccessStatusCode, $"登入失敗 HTTP {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
}

async Task<HttpResponseMessage> SendWithCsrfAsync(HttpClient client, HttpMethod method, string url, object? body)
{
    var tokenResponse = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf-token");
    var token = tokenResponse.GetProperty("token").GetString() ?? throw new InvalidOperationException("CSRF token 為空");
    using var request = new HttpRequestMessage(method, url);
    request.Headers.Add("X-CSRF-TOKEN", token);
    if (body is not null) request.Content = JsonContent.Create(body);
    return await client.SendAsync(request);
}

async Task<T> ReadJsonAsync<T>(HttpResponseMessage response)
{
    var value = await response.Content.ReadFromJsonAsync<T>();
    return value ?? throw new InvalidOperationException("JSON 回應為空");
}

async Task RunAsync(string name, Func<Task> test)
{
    try { await test(); results.Add((name, true, "PASS")); }
    catch (Exception exception) { results.Add((name, false, exception.Message)); }
}

void Ensure(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

async Task WaitForServerAsync()
{
    using var client = CreateClient();
    for (var attempt = 0; attempt < 180; attempt++)
    {
        try
        {
            var response = await client.GetAsync("/api/v1/products");
            if (response.IsSuccessStatusCode) return;
        }
        catch { }
        await Task.Delay(500);
    }
    throw new InvalidOperationException($"服務未在 {baseUrl} 啟動。");
}

async Task CleanupAsync()
{
    await using var db = new ApplicationDbContext(dbOptions);
    var testUsers = await db.Users.Where(user => user.UserName == account || user.UserName == $"m{account}").Select(user => user.Id).ToListAsync();
    var testOrders = await db.Orders.Where(order => testUsers.Contains(order.UserId)).Select(order => order.Id).ToListAsync();
    db.OrderItems.RemoveRange(await db.OrderItems.Where(item => testOrders.Contains(item.OrderId)).ToListAsync());
    db.Orders.RemoveRange(await db.Orders.Where(order => testUsers.Contains(order.UserId)).ToListAsync());
    db.UserCartItems.RemoveRange(await db.UserCartItems.Where(item => testUsers.Contains(item.UserId) || item.ProductCode == productCode).ToListAsync());
    if (testUsers.Count > 0)
    {
        db.UserRoles.RemoveRange(await db.UserRoles.Where(value => testUsers.Contains(value.UserId)).ToListAsync());
        db.Users.RemoveRange(await db.Users.Where(user => testUsers.Contains(user.Id)).ToListAsync());
    }
    var product = await db.Products.FindAsync(productCode);
    if (product is not null) db.Products.Remove(product);
    await db.SaveChangesAsync();
}

void PrintResults()
{
    Console.WriteLine();
    Console.WriteLine("=== WillyShop Integration Smoke Tests ===");
    foreach (var result in results)
        Console.WriteLine($"[{(result.Passed ? "PASS" : "FAIL")}] {result.Name}: {result.Detail}");
    Console.WriteLine($"Summary: {results.Count(result => result.Passed)}/{results.Count} passed");
}