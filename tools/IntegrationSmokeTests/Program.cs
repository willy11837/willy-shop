using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WillyShop.Data;
using WillyShop.Models;

[CompilerGenerated]
internal class Program
{
	private static async Task<int> Main(string[] args)
	{
		string baseUrl = Environment.GetEnvironmentVariable("SMOKE_BASE_URL") ?? "http://localhost:5151";
		string suffix = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
		string productCode = "IT" + suffix.Substring(suffix.Length - 10);
		string account = "it" + suffix.Substring(suffix.Length - 12);
		string email = account + "@example.test";
		string password = "TestUser@123";
		string adminAccount = "a" + account;
		string adminPassword = "AdminTest@123";
		string systemAccount = "s" + account;
		string systemPassword = "SystemTest@123";
		List<(string Name, bool Passed, string Detail)> results = new List<(string, bool, string)>();
		int? orderId = null;
		string? userId = null;
		string connectionString = "Server=(localdb)\\mssqllocaldb;Database=WebApp1Db;Trusted_Connection=True;MultipleActiveResultSets=true";
		DbContextOptions<ApplicationDbContext> dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connectionString).Options;
		PasswordHasher<ApplicationUser> passwordHasher = new PasswordHasher<ApplicationUser>();
		try
		{
			await WaitForServerAsync();
			await SeedPrivilegedUsersAsync();
			await RunAsync("公開商品 API", async () =>
			{
				using HttpClient client = CreateClient();
				HttpResponseMessage response = await client.GetAsync("/api/v1/products");
				Ensure(response.StatusCode == HttpStatusCode.OK, $"HTTP {(int)response.StatusCode}");
				JsonElement products = await ReadEnvelopeDataAsync(response);
				Ensure(products.GetProperty("items").ValueKind == JsonValueKind.Array, "items 回應不是陣列");
				Ensure(products.GetProperty("totalCount").GetInt32() >= 0, "缺少 totalCount");
			});
			await RunAsync("Vue SPA deep-link 與 API Proxy", async () =>
			{
				using HttpClient client = CreateClient();
				HttpResponseMessage response = await client.GetAsync("/profile");
				Ensure(response.StatusCode == HttpStatusCode.OK, $"deep-link HTTP {(int)response.StatusCode}");
				Ensure((await response.Content.ReadAsStringAsync()).Contains("<div id=\"app\"></div>", StringComparison.Ordinal), "未回傳 Vue index.html");
			});
			await RunAsync("匿名狀態與訂單 401", async () =>
			{
				using HttpClient client = CreateClient();
				HttpResponseMessage me = await client.GetAsync("/api/v1/auth/me");
				Ensure(me.StatusCode == HttpStatusCode.Unauthorized, $"me 預期 401，實際 {(int)me.StatusCode}");
				JsonElement meError = await ReadEnvelopeAsync(me);
				Ensure(!meError.GetProperty("success").GetBoolean() && meError.GetProperty("code").GetInt32() == 401, "me 401 envelope 錯誤");
				HttpResponseMessage orders = await client.GetAsync("/api/v1/orders");
				Ensure(orders.StatusCode == HttpStatusCode.Unauthorized, $"預期 401，實際 {(int)orders.StatusCode}");
				HttpResponseMessage subscription = await client.GetAsync("/api/v1/products/missing/back-in-stock-subscription");
				Ensure(subscription.StatusCode == HttpStatusCode.Unauthorized, $"到貨訂閱預期 401，實際 {(int)subscription.StatusCode}");
			});
			await RunAsync("寫入 API 缺少 CSRF Token 時拒絕要求", async () =>
			{
				using HttpClient client = CreateClient();
				HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/register", new
				{
					account = account,
					displayName = "整合測試會員",
					email = email,
					password = password,
					confirmPassword = password
				});
				Ensure(response.StatusCode == HttpStatusCode.BadRequest, $"預期 400，實際 {(int)response.StatusCode}");
				Ensure((await ReadEnvelopeAsync(response)).GetProperty("message").GetString() == "要求缺少有效的 CSRF Token。", "缺少 CSRF Token 時未回傳預期訊息");
			});
			HttpClient admin = CreateClient();
			try
			{
				await LoginAsync(admin, adminAccount, adminPassword);
				await RunAsync("Admin JWT 登入與角色", async () =>
				{
					Ensure((await GetEnvelopeDataAsync(admin, "/api/v1/auth/me")).GetProperty("roles").EnumerateArray().Any((JsonElement role) => role.GetString() == "Admin"), "缺少 Admin 角色");
				});
				await RunAsync("管理商品新增與 DB 寫入", async () =>
				{
					HttpResponseMessage response = await SendWithCsrfAsync(admin, HttpMethod.Post, "/api/v1/admin/products", new
					{
						productCode = productCode,
						name = "整合測試商品",
						price = 125.50m,
						category = "自動測試",
						stockQuantity = 10,
						status = 0,
						imagePath = "~/images/product-placeholder.svg"
					});
					bool condition = response.StatusCode == HttpStatusCode.Created;
					object arg = (int)response.StatusCode;
					Ensure(condition, $"HTTP {arg}: {await response.Content.ReadAsStringAsync()}");
					await using ApplicationDbContext db = new ApplicationDbContext(dbOptions);
					Product? product = await db.Products.AsNoTracking().SingleOrDefaultAsync((Product value) => value.ProductCode == productCode);
					Ensure(product != null && product.Price == 125.50m && product.StockQuantity == 10, "DB 商品資料不正確");
				});
				await RunAsync("管理商品修改與 DB 更新", async () =>
				{
					HttpResponseMessage response = await SendWithCsrfAsync(admin, HttpMethod.Put, "/api/v1/admin/products/" + productCode, new
					{
						productCode = productCode,
						name = "整合測試商品-已修改",
						price = 150m,
						category = "自動測試",
						stockQuantity = 12,
						status = 0,
						imagePath = "~/images/product-placeholder.svg"
					});
					Ensure(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}");
					await using ApplicationDbContext db = new ApplicationDbContext(dbOptions);
					Product product = await db.Products.AsNoTracking().SingleAsync((Product value) => value.ProductCode == productCode);
					Ensure(product.Name.EndsWith("已修改") && product.Price == 150m && product.StockQuantity == 12, "DB 商品修改未生效");
				});
				await RunAsync("商品搜尋、排序、分頁與明細", async () =>
				{
					using HttpClient client = CreateClient();
					JsonElement list = await GetEnvelopeDataAsync(client, $"/api/v1/products?productCode={productCode}&productName={Uri.EscapeDataString("整合測試")}&category={Uri.EscapeDataString("自動測試")}&sortBy=price&sortDirection=desc&page=1&pageSize=1");
					Ensure(list.GetProperty("page").GetInt32() == 1 && list.GetProperty("pageSize").GetInt32() == 1, "分頁資料錯誤");
					Ensure(list.GetProperty("totalCount").GetInt32() == 1, "搜尋結果筆數錯誤");
					JsonElement item = list.GetProperty("items")[0];
					Ensure(item.GetProperty("productCode").GetString() == productCode && item.GetProperty("price").GetDecimal() == 150m, "搜尋或排序結果錯誤");
					JsonElement detail = await GetEnvelopeDataAsync(client, "/api/v1/products/" + productCode);
					Ensure(detail.GetProperty("name").GetString()?.EndsWith("已修改") ?? false, "商品明細錯誤");
					Ensure(detail.GetProperty("imageUrl").GetString() == "/images/product-placeholder.svg", "商品圖片 URL 未正規化");
				});
				HttpClient shopper = CreateClient();
				try
				{
					await RunAsync("匿名購物車 Session 新增與更新", async () =>
					{
						HttpResponseMessage add = await SendWithCsrfAsync(shopper, HttpMethod.Post, "/api/v1/cart/items/" + productCode, new
						{
							quantity = 2
						});
						Ensure(add.IsSuccessStatusCode, $"新增 HTTP {(int)add.StatusCode}");
						Ensure((await ReadEnvelopeDataAsync(add)).GetProperty("totalQuantity").GetInt32() == 2, "匿名購物車新增數量錯誤");
						Ensure((await ReadEnvelopeDataAsync(await SendWithCsrfAsync(shopper, HttpMethod.Put, "/api/v1/cart/items/" + productCode, new
						{
							quantity = 3
						}))).GetProperty("totalQuantity").GetInt32() == 3, "匿名購物車更新數量錯誤");
					});
					await RunAsync("註冊、Normal 角色與匿名購物車合併", async () =>
					{
						HttpResponseMessage response = await SendWithCsrfAsync(shopper, HttpMethod.Post, "/api/v1/auth/register", new
						{
							account = account,
							displayName = "整合測試會員",
							email = email,
							password = password,
							confirmPassword = password
						});
						bool isSuccessStatusCode = response.IsSuccessStatusCode;
						object arg = (int)response.StatusCode;
						Ensure(isSuccessStatusCode, $"HTTP {arg}: {await response.Content.ReadAsStringAsync()}");
						JsonElement registered = await ReadEnvelopeDataAsync(response);
						SetBearerFromAuthResponse(shopper, registered);
						JsonElement registeredUser = registered.GetProperty("user");
						userId = registeredUser.GetProperty("id").GetString()
							?? throw new InvalidOperationException("註冊使用者 ID 為空");
						Ensure(registeredUser.GetProperty("roles").EnumerateArray().Any((JsonElement role) => role.GetString() == "Normal"), "缺少 Normal 角色");
						Ensure((await GetEnvelopeDataAsync(shopper, "/api/v1/cart")).GetProperty("totalQuantity").GetInt32() == 3, "購物車未合併");
						await using ApplicationDbContext db = new ApplicationDbContext(dbOptions);
						UserCartItem? userCartItem = await db.UserCartItems.AsNoTracking().SingleOrDefaultAsync((UserCartItem item) => item.UserId == userId && item.ProductCode == productCode);
						Ensure(userCartItem != null && userCartItem.Quantity == 3, "DB 會員購物車數量錯誤");
					});
					await RunAsync("重複帳號回傳 409 與後端訊息", async () =>
					{
						using HttpClient duplicateClient = CreateClient();
						HttpResponseMessage response = await SendWithCsrfAsync(duplicateClient, HttpMethod.Post, "/api/v1/auth/register", new
						{
							account = account,
							displayName = "重複帳號",
							email = "duplicate-" + email,
							password = password,
							confirmPassword = password
						});
						Ensure(response.StatusCode == HttpStatusCode.Conflict, $"預期 409，實際 {(int)response.StatusCode}");
						JsonElement error = await ReadEnvelopeAsync(response);
						Ensure(!error.GetProperty("success").GetBoolean(), "重複帳號 envelope.success 應為 false");
						Ensure(error.GetProperty("message").GetString() == "此使用者帳號已被使用。", "重複帳號未回傳預期 message");
					});
					await RunAsync("Profile 讀取、更新與 DB 寫入", async () =>
					{
						Ensure((await GetEnvelopeDataAsync(shopper, "/api/v1/profile")).GetProperty("account").GetString() == account, "Profile 帳號錯誤");
						string updatedEmail = "updated-" + email;
						HttpResponseMessage response = await SendWithCsrfAsync(shopper, HttpMethod.Put, "/api/v1/profile", new
						{
							displayName = "整合測試會員-已修改",
							email = updatedEmail
						});
						bool isSuccessStatusCode = response.IsSuccessStatusCode;
						object arg = (int)response.StatusCode;
						Ensure(isSuccessStatusCode, $"HTTP {arg}: {await response.Content.ReadAsStringAsync()}");
						Ensure((await ReadEnvelopeDataAsync(response)).GetProperty("displayName").GetString()?.EndsWith("已修改") ?? false, "Profile 回應未更新");
						await using ApplicationDbContext db = new ApplicationDbContext(dbOptions);
						ApplicationUser dbUser = await db.Users.AsNoTracking().SingleAsync((ApplicationUser user) => user.Id == userId);
						Ensure((dbUser.DisplayName?.EndsWith("已修改") ?? false) && dbUser.Email == updatedEmail, "DB Profile 未更新");
					});
					await RunAsync("缺貨訂閱冪等、到貨通知與使用者隔離", async () =>
					{
						HttpResponseMessage outOfStock = await SendWithCsrfAsync(admin, HttpMethod.Put, "/api/v1/admin/products/" + productCode, new
						{
							productCode = productCode,
							name = "整合測試商品-已修改",
							price = 150m,
							isOnSale = true,
							salePrice = 120m,
							category = "自動測試",
							stockQuantity = 0,
							status = 2,
							imagePath = "~/images/product-placeholder.svg"
						});
						Ensure(outOfStock.IsSuccessStatusCode, $"設定缺貨 HTTP {(int)outOfStock.StatusCode}");

						for (int attempt = 0; attempt < 2; attempt++)
						{
							HttpResponseMessage subscribe = await SendWithCsrfAsync(shopper, HttpMethod.Post, "/api/v1/products/" + productCode + "/back-in-stock-subscription", null);
							Ensure(subscribe.StatusCode == HttpStatusCode.Created, $"訂閱 HTTP {(int)subscribe.StatusCode}");
						}
						await using (ApplicationDbContext db = new ApplicationDbContext(dbOptions))
						{
							int activeSubscriptions = await db.BackInStockSubscriptions.CountAsync(value => value.UserId == userId && value.ProductCode == productCode && value.NotifiedAt == null);
							Ensure(activeSubscriptions == 1, $"有效訂閱應為 1，實際 {activeSubscriptions}");
						}

						HttpResponseMessage frozen = await SendWithCsrfAsync(admin, HttpMethod.Put, "/api/v1/admin/products/" + productCode, new
						{
							productCode = productCode,
							name = "整合測試商品-已修改",
							price = 150m,
							category = "自動測試",
							stockQuantity = 0,
							status = 1,
							imagePath = "~/images/product-placeholder.svg"
						});
						Ensure(frozen.IsSuccessStatusCode, $"設定凍結 HTTP {(int)frozen.StatusCode}");
						await using (ApplicationDbContext db = new ApplicationDbContext(dbOptions))
						{
							Ensure(await db.UserNotifications.CountAsync(value => value.UserId == userId) == 0, "缺貨轉凍結不應建立通知");
						}

						HttpResponseMessage available = await SendWithCsrfAsync(admin, HttpMethod.Put, "/api/v1/admin/products/" + productCode, new
						{
							productCode = productCode,
							name = "整合測試商品-已修改",
							price = 150m,
							isOnSale = true,
							salePrice = 120m,
							category = "自動測試",
							stockQuantity = 12,
							status = 0,
							imagePath = "~/images/product-placeholder.svg"
						});
						Ensure(available.IsSuccessStatusCode, $"恢復供貨 HTTP {(int)available.StatusCode}");
						JsonElement unread = await GetEnvelopeDataAsync(shopper, "/api/v1/notifications/unread-count");
						Ensure(unread.GetProperty("count").GetInt32() == 1, "未讀通知數應為 1");
						JsonElement notificationList = await GetEnvelopeDataAsync(shopper, "/api/v1/notifications?page=1&pageSize=20");
						JsonElement notification = notificationList.GetProperty("items")[0];
						long notificationId = notification.GetProperty("id").GetInt64();
						Ensure(notification.GetProperty("productCode").GetString() == productCode, "通知商品代碼錯誤");
						HttpResponseMessage forbiddenRead = await SendWithCsrfAsync(admin, HttpMethod.Put, $"/api/v1/notifications/{notificationId}/read", null);
						Ensure(forbiddenRead.StatusCode == HttpStatusCode.Forbidden, $"他人通知預期 403，實際 {(int)forbiddenRead.StatusCode}");
						HttpResponseMessage markRead = await SendWithCsrfAsync(shopper, HttpMethod.Put, $"/api/v1/notifications/{notificationId}/read", null);
						Ensure(markRead.IsSuccessStatusCode, $"標示已讀 HTTP {(int)markRead.StatusCode}");
						Ensure((await GetEnvelopeDataAsync(shopper, "/api/v1/notifications/unread-count")).GetProperty("count").GetInt32() == 0, "標示已讀後未讀數不為 0");
					});
					await RunAsync("再次缺貨可重新訂閱與全部已讀", async () =>
					{
						HttpResponseMessage outOfStock = await SendWithCsrfAsync(admin, HttpMethod.Put, "/api/v1/admin/products/" + productCode, new
						{
							productCode = productCode,
							name = "整合測試商品-已修改",
							price = 150m,
							category = "自動測試",
							stockQuantity = 0,
							status = 2,
							imagePath = "~/images/product-placeholder.svg"
						});
						Ensure(outOfStock.IsSuccessStatusCode, $"再次設定缺貨 HTTP {(int)outOfStock.StatusCode}");
						Ensure((await SendWithCsrfAsync(shopper, HttpMethod.Post, "/api/v1/products/" + productCode + "/back-in-stock-subscription", null)).StatusCode == HttpStatusCode.Created, "再次訂閱失敗");
						HttpResponseMessage available = await SendWithCsrfAsync(admin, HttpMethod.Put, "/api/v1/admin/products/" + productCode, new
						{
							productCode = productCode,
							name = "整合測試商品-已修改",
							price = 150m,
							isOnSale = true,
							salePrice = 120m,
							category = "自動測試",
							stockQuantity = 12,
							status = 0,
							imagePath = "~/images/product-placeholder.svg"
						});
						Ensure(available.IsSuccessStatusCode, $"再次恢復供貨 HTTP {(int)available.StatusCode}");
						JsonElement saleDetail = await GetEnvelopeDataAsync(shopper, "/api/v1/products/" + productCode);
						Ensure(saleDetail.GetProperty("isOnSale").GetBoolean()
							&& saleDetail.GetProperty("price").GetDecimal() == 150m
							&& saleDetail.GetProperty("salePrice").GetDecimal() == 120m
							&& saleDetail.GetProperty("effectivePrice").GetDecimal() == 120m,
							"公開商品 API 特價資料錯誤");
						JsonElement saleProducts = await GetEnvelopeDataAsync(shopper, "/api/v1/products/on-sale");
						Ensure(saleProducts.EnumerateArray().Any(item => item.GetProperty("productCode").GetString() == productCode), "特價商品清單缺少測試商品");
						HttpResponseMessage invalidSale = await SendWithCsrfAsync(admin, HttpMethod.Put, "/api/v1/admin/products/" + productCode, new
						{
							productCode = productCode,
							name = "整合測試商品-已修改",
							price = 150m,
							isOnSale = true,
							salePrice = 150m,
							category = "自動測試",
							stockQuantity = 12,
							status = 0,
							imagePath = "~/images/product-placeholder.svg"
						});
						Ensure(invalidSale.StatusCode == HttpStatusCode.BadRequest, $"無效特價預期 400，實際 {(int)invalidSale.StatusCode}");
						Ensure((await GetEnvelopeDataAsync(shopper, "/api/v1/notifications/unread-count")).GetProperty("count").GetInt32() == 1, "再次到貨未讀數應為 1");
						Ensure((await SendWithCsrfAsync(shopper, HttpMethod.Put, "/api/v1/notifications/read-all", null)).IsSuccessStatusCode, "全部已讀失敗");
						Ensure((await GetEnvelopeDataAsync(shopper, "/api/v1/notifications/unread-count")).GetProperty("count").GetInt32() == 0, "全部已讀後未讀數不為 0");
					});
					await RunAsync("Refresh Token rotation 與新 JWT", async () =>
					{
						await using ApplicationDbContext beforeDb = new ApplicationDbContext(dbOptions);
						List<RefreshToken> before = await (from token in beforeDb.RefreshTokens.AsNoTracking()
							where token.UserId == userId
							select token).ToListAsync();
						Ensure(before.Count == 1 && !before[0].RevokedAt.HasValue, "初始 Refresh Token 狀態錯誤");
						HttpResponseMessage response = await SendWithCsrfAsync(shopper, HttpMethod.Post, "/api/v1/auth/refresh", null);
						bool isSuccessStatusCode = response.IsSuccessStatusCode;
						object arg = (int)response.StatusCode;
						Ensure(isSuccessStatusCode, $"HTTP {arg}: {await response.Content.ReadAsStringAsync()}");
						HttpClient client = shopper;
						SetBearerFromAuthResponse(client, await ReadEnvelopeDataAsync(response));
						Ensure((await GetEnvelopeDataAsync(shopper, "/api/v1/auth/me")).GetProperty("account").GetString() == account, "rotation 後 JWT 無法使用");
						await using ApplicationDbContext afterDb = new ApplicationDbContext(dbOptions);
						List<RefreshToken> after = await (from token in afterDb.RefreshTokens.AsNoTracking()
							where token.UserId == userId
							orderby token.Id
							select token).ToListAsync();
						Ensure(after.Count == 2, "rotation 後 Refresh Token 數量錯誤");
						Ensure(Enumerable.Count(after, (RefreshToken token) => !token.RevokedAt.HasValue) == 1 && Enumerable.Count(after, (RefreshToken token) => token.ReplacedByTokenId.HasValue) == 1, "rotation 後 DB token 狀態錯誤");
					});
					await RunAsync("Normal 使用者禁止管理 API", async () =>
					{
						HttpResponseMessage response = await shopper.GetAsync("/api/v1/admin/products");
						Ensure(response.StatusCode == HttpStatusCode.Forbidden, $"預期 403，實際 {(int)response.StatusCode}");
					});
					await RunAsync("會員購物車刪除與重新加入", async () =>
					{
						Ensure((await ReadEnvelopeDataAsync(await SendWithCsrfAsync(shopper, HttpMethod.Delete, "/api/v1/cart/items/" + productCode, null))).GetProperty("totalQuantity").GetInt32() == 0, "移除失敗");
						Ensure((await ReadEnvelopeDataAsync(await SendWithCsrfAsync(shopper, HttpMethod.Post, "/api/v1/cart/items/" + productCode, new
						{
							quantity = 4
						}))).GetProperty("totalAmount").GetDecimal() == 480m, "會員購物車未採用特價金額");
					});
					await RunAsync("結帳、訂單明細、庫存扣減與購物車清空", async () =>
					{
						HttpResponseMessage response = await SendWithCsrfAsync(shopper, HttpMethod.Post, "/api/v1/orders/checkout", null);
						bool isSuccessStatusCode = response.IsSuccessStatusCode;
						object arg = (int)response.StatusCode;
						Ensure(isSuccessStatusCode, $"HTTP {arg}: {await response.Content.ReadAsStringAsync()}");
						JsonElement order = await ReadEnvelopeDataAsync(response);
						orderId = order.GetProperty("id").GetInt32();
						Ensure(order.GetProperty("totalAmount").GetDecimal() == 480m, "訂單總額未採用特價");
						Ensure(order.GetProperty("items")[0].GetProperty("quantity").GetInt32() == 4
							&& order.GetProperty("items")[0].GetProperty("unitPrice").GetDecimal() == 120m,
							"訂單明細未保存特價單價");
						await using ApplicationDbContext db = new ApplicationDbContext(dbOptions);
						Order? dbOrder = await db.Orders.AsNoTracking().SingleOrDefaultAsync((Order value) => (int?)value.Id == orderId && value.UserId == userId);
						OrderItem? dbItem = await db.OrderItems.AsNoTracking().SingleOrDefaultAsync((OrderItem value) => (int?)value.OrderId == orderId && value.ProductCode == productCode);
						Product product = await db.Products.AsNoTracking().SingleAsync((Product value) => value.ProductCode == productCode);
						int num = await db.UserCartItems.CountAsync((UserCartItem value) => value.UserId == userId);
						Ensure(dbOrder != null && dbOrder.TotalAmount == 480m, "DB 訂單錯誤");
						Ensure(dbItem != null && dbItem.Quantity == 4 && dbItem.UnitPrice == 120m && dbItem.Subtotal == 480m, "DB 訂單明細錯誤");
						Ensure(product.StockQuantity == 8, $"庫存應為 8，實際 {product.StockQuantity}");
						Ensure(num == 0, "結帳後 DB 購物車未清空");
					});
					await RunAsync("訂單列表與單筆權限", async () =>
					{
						Ensure((await GetEnvelopeDataAsync(shopper, "/api/v1/orders")).EnumerateArray().Any((JsonElement order) => order.GetProperty("id").GetInt32() == orderId), "訂單列表找不到新訂單");
						Ensure((await shopper.GetAsync($"/api/v1/orders/{orderId}")).IsSuccessStatusCode, "本人無法取得訂單");
						Ensure((await admin.GetAsync($"/api/v1/orders/{orderId}")).IsSuccessStatusCode, "Admin 無法取得訂單");
					});
					await RunAsync("Admin 帳號管理新增、修改", async () =>
					{
						string managedAccount = "m" + account;
						string managedEmail = "m" + email;
						HttpResponseMessage create = await SendWithCsrfAsync(admin, HttpMethod.Post, "/api/v1/admin/users", new
						{
							account = managedAccount,
							displayName = "受管理測試帳號",
							email = managedEmail,
							password = password,
							role = "Guest"
						});
						bool isSuccessStatusCode = create.IsSuccessStatusCode;
						object arg = (int)create.StatusCode;
						Ensure(isSuccessStatusCode, $"新增 HTTP {arg}: {await create.Content.ReadAsStringAsync()}");
						string managedId = (await ReadEnvelopeDataAsync(create)).GetProperty("id").GetString()
							?? throw new InvalidOperationException("受管理帳號 ID 為空");
						HttpResponseMessage update = await SendWithCsrfAsync(admin, HttpMethod.Put, "/api/v1/admin/users/" + managedId, new
						{
							displayName = "受管理測試帳號-已修改",
							email = managedEmail,
							newPassword = "Changed@123",
							role = "Normal"
						});
						Ensure(update.IsSuccessStatusCode, $"修改 HTTP {(int)update.StatusCode}");
						await using ApplicationDbContext db = new ApplicationDbContext(dbOptions);
						ApplicationUser dbUser = await db.Users.AsNoTracking().SingleAsync((ApplicationUser user) => user.Id == managedId);
						string normalRoleId = await (from role in db.Roles
							where role.Name == "Normal"
							select role.Id).SingleAsync();
						Ensure(dbUser.DisplayName?.EndsWith("已修改") ?? false, "DB 帳號修改未生效");
						Ensure(await db.UserRoles.AnyAsync((IdentityUserRole<string> value) => value.UserId == managedId && value.RoleId == normalRoleId), "DB 角色修改未生效");
					});
					await RunAsync("帳號刪除角色限制與 System 刪除", async () =>
					{
						await using ApplicationDbContext lookup = new ApplicationDbContext(dbOptions);
						string managedId = await (from user in lookup.Users
							where user.UserName == $"m{account}"
							select user.Id).SingleAsync();
						HttpResponseMessage adminDelete = await SendWithCsrfAsync(admin, HttpMethod.Delete, "/api/v1/admin/users/" + managedId, null);
						Ensure(adminDelete.StatusCode == HttpStatusCode.Forbidden, $"Admin 刪除預期 403，實際 {(int)adminDelete.StatusCode}");
						using HttpClient system = CreateClient();
						await LoginAsync(system, systemAccount, systemPassword);
						HttpResponseMessage systemDelete = await SendWithCsrfAsync(system, HttpMethod.Delete, "/api/v1/admin/users/" + managedId, null);
						Ensure(systemDelete.StatusCode == HttpStatusCode.OK, $"System 刪除 HTTP {(int)systemDelete.StatusCode}");
						await using ApplicationDbContext verify = new ApplicationDbContext(dbOptions);
						Ensure(!(await verify.Users.AnyAsync((ApplicationUser user) => user.Id == managedId)), "DB 帳號未刪除");
					});
					await RunAsync("商品刪除同步清除購物車參照", async () =>
					{
						await using (ApplicationDbContext db = new ApplicationDbContext(dbOptions))
						{
							db.UserCartItems.Add(new UserCartItem
							{
								UserId = userId ?? throw new InvalidOperationException("註冊使用者 ID 為空"),
								ProductCode = productCode,
								Quantity = 1
							});
							await db.SaveChangesAsync();
						}
						HttpResponseMessage delete = await SendWithCsrfAsync(admin, HttpMethod.Delete, "/api/v1/admin/products/" + productCode, null);
						Ensure(delete.StatusCode == HttpStatusCode.OK, $"HTTP {(int)delete.StatusCode}");
						await using ApplicationDbContext verify = new ApplicationDbContext(dbOptions);
						Ensure(!(await verify.Products.AnyAsync((Product value) => value.ProductCode == productCode)), "商品未刪除");
						Ensure(!(await verify.UserCartItems.AnyAsync((UserCartItem value) => value.ProductCode == productCode)), "購物車參照未清除");
					});
					await RunAsync("個人資料修改密碼與重新登入", async () =>
					{
						string newPassword = "NewTestUser@456";
						HttpResponseMessage wrongCurrentPassword = await SendWithCsrfAsync(shopper, HttpMethod.Post, "/api/v1/profile/change-password", new
						{
							currentPassword = "WrongPassword@123",
							newPassword,
							confirmPassword = newPassword
						});
						Ensure(wrongCurrentPassword.StatusCode == HttpStatusCode.BadRequest, $"錯誤目前密碼預期 400，實際 {(int)wrongCurrentPassword.StatusCode}");

						HttpResponseMessage changed = await SendWithCsrfAsync(shopper, HttpMethod.Post, "/api/v1/profile/change-password", new
						{
							currentPassword = password,
							newPassword,
							confirmPassword = newPassword
						});
						Ensure(changed.StatusCode == HttpStatusCode.OK, $"修改密碼 HTTP {(int)changed.StatusCode}: {await changed.Content.ReadAsStringAsync()}");

						await using (ApplicationDbContext db = new ApplicationDbContext(dbOptions))
						{
							ApplicationUser dbUser = await db.Users.AsNoTracking().SingleAsync(user => user.Id == userId);
							Ensure(passwordHasher.VerifyHashedPassword(dbUser, dbUser.PasswordHash!, password) == PasswordVerificationResult.Failed, "舊密碼仍可驗證");
							Ensure(passwordHasher.VerifyHashedPassword(dbUser, dbUser.PasswordHash!, newPassword) != PasswordVerificationResult.Failed, "新密碼無法驗證");
							Ensure(dbUser.ModifiedAt.HasValue, "修改密碼未更新最後修改時間");
						}

						using HttpClient oldPasswordClient = CreateClient();
						HttpResponseMessage oldLogin = await SendWithCsrfAsync(oldPasswordClient, HttpMethod.Post, "/api/v1/auth/login", new { account, password });
						Ensure(oldLogin.StatusCode == HttpStatusCode.Unauthorized, $"舊密碼登入預期 401，實際 {(int)oldLogin.StatusCode}");

						using HttpClient newPasswordClient = CreateClient();
						await LoginAsync(newPasswordClient, account, newPassword);
						Ensure((await GetEnvelopeDataAsync(newPasswordClient, "/api/v1/auth/me")).GetProperty("account").GetString() == account, "新密碼登入後無法取得會員資料");
					});
					await RunAsync("登出、Refresh Token 撤銷與 Cookie 清除", async () =>
					{
						HttpResponseMessage response = await SendWithCsrfAsync(shopper, HttpMethod.Post, "/api/v1/auth/logout", null);
						Ensure(response.StatusCode == HttpStatusCode.OK, $"HTTP {(int)response.StatusCode}");
						shopper.DefaultRequestHeaders.Authorization = null;
						HttpResponseMessage refresh = await SendWithCsrfAsync(shopper, HttpMethod.Post, "/api/v1/auth/refresh", null);
						Ensure(refresh.StatusCode == HttpStatusCode.Unauthorized, $"登出後 refresh 預期 401，實際 {(int)refresh.StatusCode}");
						Ensure((await shopper.GetAsync("/api/v1/auth/me")).StatusCode == HttpStatusCode.Unauthorized, "登出後 me 仍可存取");
					});
				}
				finally
				{
					if (shopper != null)
					{
						((IDisposable)shopper).Dispose();
					}
				}
			}
			finally
			{
				if (admin != null)
				{
					((IDisposable)admin).Dispose();
				}
			}
		}
		finally
		{
			try
			{
				await CleanupAsync();
				await using ApplicationDbContext verifyCleanup = new ApplicationDbContext(dbOptions);
				string[] cleanupAccounts = new string[4]
				{
					account,
					"m" + account,
					adminAccount,
					systemAccount
				};
				bool hasUsers = await verifyCleanup.Users.AnyAsync((ApplicationUser user) => Enumerable.Contains(cleanupAccounts, user.UserName));
				bool hasProduct = await verifyCleanup.Products.AnyAsync((Product product) => product.ProductCode == productCode);
				results.Add(("測試資料清理與 DB 無殘留", !hasUsers && !hasProduct, (hasUsers | hasProduct) ? "仍有測試資料殘留" : "PASS"));
			}
			catch (Exception ex)
			{
				results.Add(("測試資料清理與 DB 無殘留", false, ex.Message));
			}
			PrintResults();
		}
		return (!results.All(((string Name, bool Passed, string Detail) result) => result.Passed)) ? 1 : 0;
		async Task CleanupAsync()
		{
			await using ApplicationDbContext db = new ApplicationDbContext(dbOptions);
			string[] testAccounts = new string[4]
			{
				account,
				"m" + account,
				adminAccount,
				systemAccount
			};
			List<string> testUsers = await (from user in db.Users
				where Enumerable.Contains(testAccounts, user.UserName)
				select user.Id).ToListAsync();
			List<int> testOrders = await (from order in db.Orders
				where testUsers.Contains(order.UserId)
				select order.Id).ToListAsync();
			DbSet<OrderItem> orderItems = db.OrderItems;
			orderItems.RemoveRange(await db.OrderItems.Where((OrderItem item) => testOrders.Contains(item.OrderId)).ToListAsync());
			DbSet<Order> orders = db.Orders;
			orders.RemoveRange(await db.Orders.Where((Order order) => testUsers.Contains(order.UserId)).ToListAsync());
			DbSet<UserCartItem> userCartItems = db.UserCartItems;
			userCartItems.RemoveRange(await db.UserCartItems.Where((UserCartItem item) => testUsers.Contains(item.UserId) || item.ProductCode == productCode).ToListAsync());
			if (testUsers.Count > 0)
			{
				DbSet<RefreshToken> refreshTokens = db.RefreshTokens;
				refreshTokens.RemoveRange(await db.RefreshTokens.Where((RefreshToken value) => testUsers.Contains(value.UserId)).ToListAsync());
				DbSet<IdentityUserClaim<string>> userClaims = db.UserClaims;
				userClaims.RemoveRange(await db.UserClaims.Where((IdentityUserClaim<string> value) => testUsers.Contains(value.UserId)).ToListAsync());
				DbSet<IdentityUserLogin<string>> userLogins = db.UserLogins;
				userLogins.RemoveRange(await db.UserLogins.Where((IdentityUserLogin<string> value) => testUsers.Contains(value.UserId)).ToListAsync());
				DbSet<IdentityUserToken<string>> userTokens = db.UserTokens;
				userTokens.RemoveRange(await db.UserTokens.Where((IdentityUserToken<string> value) => testUsers.Contains(value.UserId)).ToListAsync());
				DbSet<IdentityUserRole<string>> userRoles = db.UserRoles;
				userRoles.RemoveRange(await db.UserRoles.Where((IdentityUserRole<string> value) => testUsers.Contains(value.UserId)).ToListAsync());
				DbSet<ApplicationUser> users = db.Users;
				users.RemoveRange(await db.Users.Where((ApplicationUser user) => testUsers.Contains(user.Id)).ToListAsync());
			}
			Product? product = await db.Products.FindAsync(productCode);
			if (product != null)
			{
				db.Products.Remove(product);
			}
			await db.SaveChangesAsync();
		}
		HttpClient CreateClient()
		{
			return new HttpClient(new HttpClientHandler
			{
				CookieContainer = new CookieContainer(),
				AllowAutoRedirect = false
			})
			{
				BaseAddress = new Uri(baseUrl),
				Timeout = TimeSpan.FromSeconds(20.0)
			};
		}
		ApplicationUser CreateUser(string userName, string displayName)
		{
			ApplicationUser user = new ApplicationUser
			{
				UserName = userName,
				NormalizedUserName = userName.ToUpperInvariant(),
				Email = userName + "@example.test",
				NormalizedEmail = (userName + "@example.test").ToUpperInvariant(),
				DisplayName = displayName,
				EmailConfirmed = true,
				SecurityStamp = Guid.NewGuid().ToString(),
				ConcurrencyStamp = Guid.NewGuid().ToString(),
				CreatedAt = DateTime.UtcNow
			};
			user.PasswordHash = passwordHasher.HashPassword(user, (userName == adminAccount) ? adminPassword : systemPassword);
			return user;
		}
		static void Ensure(bool condition, string message)
		{
			if (!condition)
			{
				throw new InvalidOperationException(message);
			}
		}
		static async Task<JsonElement> GetEnvelopeDataAsync(HttpClient client, string url)
		{
			HttpResponseMessage response = await client.GetAsync(url);
			bool isSuccessStatusCode = response.IsSuccessStatusCode;
			object arg = (int)response.StatusCode;
			Ensure(isSuccessStatusCode, $"GET {url} HTTP {arg}: {await response.Content.ReadAsStringAsync()}");
			return await ReadEnvelopeDataAsync(response);
		}
		static async Task LoginAsync(HttpClient client, string loginAccount, string loginPassword)
		{
			HttpResponseMessage response = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/auth/login", new
			{
				account = loginAccount,
				password = loginPassword
			});
			bool isSuccessStatusCode = response.IsSuccessStatusCode;
			object arg = (int)response.StatusCode;
			Ensure(isSuccessStatusCode, $"登入失敗 HTTP {arg}: {await response.Content.ReadAsStringAsync()}");
			SetBearerFromAuthResponse(client, await ReadEnvelopeDataAsync(response));
		}
		void PrintResults()
		{
			Console.WriteLine();
			Console.WriteLine("=== WillyShop Integration Smoke Tests ===");
			foreach (var result in results)
			{
				Console.WriteLine($"[{(result.Passed ? "PASS" : "FAIL")}] {result.Name}: {result.Detail}");
			}
			Console.WriteLine($"Summary: {Enumerable.Count(results, ((string Name, bool Passed, string Detail) tuple) => tuple.Passed)}/{results.Count} passed");
		}
		static async Task<JsonElement> ReadEnvelopeAsync(HttpResponseMessage response)
		{
			JsonElement envelope = await ReadJsonAsync<JsonElement>(response);
			Ensure(envelope.GetProperty("code").GetInt32() == (int)response.StatusCode, "Envelope code 與 HTTP status 不一致");
			Ensure(envelope.GetProperty("message").ValueKind == JsonValueKind.String, "Envelope 缺少 message");
			return envelope;
		}
		static async Task<JsonElement> ReadEnvelopeDataAsync(HttpResponseMessage response)
		{
			JsonElement envelope = await ReadEnvelopeAsync(response);
			Ensure(envelope.GetProperty("success").GetBoolean(), "API 回應失敗：" + envelope.GetProperty("message").GetString());
			return envelope.GetProperty("data");
		}
		static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response) where T : notnull
		{
			T? val = await response.Content.ReadFromJsonAsync<T>();
			if (val == null)
			{
				throw new InvalidOperationException("JSON 回應為空");
			}
			return val;
		}
		async Task RunAsync(string name, Func<Task> test)
		{
			try
			{
				await test();
				results.Add((name, true, "PASS"));
			}
			catch (Exception ex2)
			{
				results.Add((name, false, ex2.Message));
			}
		}
		async Task SeedPrivilegedUsersAsync()
		{
			await using ApplicationDbContext db = new ApplicationDbContext(dbOptions);
			Dictionary<string, string> roleIds = await db.Roles
				.Where((IdentityRole role) => role.Name == "Admin" || role.Name == "System")
				.ToDictionaryAsync((IdentityRole role) => role.Name!, (IdentityRole role) => role.Id);
			Ensure(roleIds.ContainsKey("Admin") && roleIds.ContainsKey("System"), "缺少管理角色種子資料");
			ApplicationUser adminUser = CreateUser(adminAccount, "整合測試 Admin");
			ApplicationUser systemUser = CreateUser(systemAccount, "整合測試 System");
			db.Users.AddRange(adminUser, systemUser);
			db.UserRoles.AddRange(new IdentityUserRole<string>
			{
				UserId = adminUser.Id,
				RoleId = roleIds["Admin"]
			}, new IdentityUserRole<string>
			{
				UserId = systemUser.Id,
				RoleId = roleIds["System"]
			});
			await db.SaveChangesAsync();
		}
		static async Task<HttpResponseMessage> SendWithCsrfAsync(HttpClient client, HttpMethod method, string url, object? body)
		{
			string token = (await GetEnvelopeDataAsync(client, "/api/v1/auth/csrf-token")).GetProperty("token").GetString() ?? throw new InvalidOperationException("CSRF token 為空");
			using HttpRequestMessage request = new HttpRequestMessage(method, url);
			request.Headers.Add("X-CSRF-TOKEN", token);
			if (body != null)
			{
				request.Content = JsonContent.Create(body);
			}
			return await client.SendAsync(request);
		}
		static void SetBearerFromAuthResponse(HttpClient client, JsonElement response)
		{
			string accessToken = response.GetProperty("accessToken").GetString() ?? throw new InvalidOperationException("Access Token 為空");
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		}
		async Task WaitForServerAsync()
		{
			using HttpClient client = CreateClient();
			for (int attempt = 0; attempt < 180; attempt++)
			{
				try
				{
					if ((await client.GetAsync("/api/v1/products")).IsSuccessStatusCode)
					{
						return;
					}
				}
				catch
				{
				}
				await Task.Delay(500);
			}
			throw new InvalidOperationException("服務未在 " + baseUrl + " 啟動。");
		}
	}
}
