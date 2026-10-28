using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using WebApplication1.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

// 中文註解：註冊 ASP.NET Core Identity 作為應用程式的認證/授權系統，並使用 ApplicationDbContext 作為儲存層。
builder.Services.AddDefaultIdentity<WebApplication1.Models.ApplicationUser>(options =>
{
    // 中文提示：設定密碼強度要求，可依需求調整
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true; // 必須包含數字
    options.Password.RequireLowercase = true; // 必須包含小寫字母
    options.Password.RequireUppercase = true; // 必須包含大寫字母
    options.Password.RequireNonAlphanumeric = false; // 可選：是否需要特殊字元
    options.Password.RequiredLength = 8; // 最少長度
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.User.RequireUniqueEmail = true;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// 中文提示：注入客製化的 IdentityErrorDescriber 以回傳中文驗證錯誤訊息
builder.Services.AddScoped<Microsoft.AspNetCore.Identity.IdentityErrorDescriber, WebApplication1.Models.ChineseIdentityErrorDescriber>();

// 中文註解：啟用記憶式快取與 Session，用於儲存匿名購物車（AnonCart）。
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(4);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// 中文註解：註冊購物車服務（處理匿名購物車與使用者購物車的合併邏輯）。
builder.Services.AddScoped<WebApplication1.Services.CartService>();
builder.Services.AddScoped<WebApplication1.Services.CartApplicationService>();
builder.Services.AddScoped<WebApplication1.Services.OrderApplicationService>();
builder.Services.AddScoped<WebApplication1.Data.ProductRepository>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// 中文註解：啟用 Session 與 Authentication/Authorization 中介軟體。
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

// 中文註解：將網站根路由導向商品列表頁，讓 / 直接顯示 Products 頁面。
app.MapGet("/", async context =>
{
    context.Response.Redirect("/Products");
    await Task.CompletedTask;
});

// 中文註解：啟動時執行資料種子，包括建立 Admin 角色與範例商品資料。
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        // 以 Migration 維護資料庫結構。
        db.Database.Migrate();

        // 僅在商品不存在時建立範例資料，避免覆寫後台修改內容。
        void AddProductIfMissing(string code, string name, string category, int qty, WebApplication1.Models.ProductStatus status, decimal price, string imagePath)
        {
            var existing = db.Products.FirstOrDefault(p => p.ProductCode == code);
            if (existing == null)
            {
                db.Products.Add(new WebApplication1.Models.Product
                {
                    ProductCode = code, Name = name, Category = category, StockQuantity = qty,
                    Status = status, Price = price, ImagePath = imagePath, CreatedAt = DateTime.UtcNow,
                    ModifiedBy = "System", ModifiedAt = DateTime.UtcNow
                });
            }
        }

        AddProductIfMissing("P001", "灰色室內拖鞋", "拖鞋", 10, WebApplication1.Models.ProductStatus.可銷售, 199m, "~/images/slipper1.jpg");
        AddProductIfMissing("P002", "黑色室外拖鞋", "拖鞋", 5, WebApplication1.Models.ProductStatus.可銷售, 249m, "~/images/slipper2.jpg");
        AddProductIfMissing("P003", "藍色防滑拖鞋", "拖鞋", 8, WebApplication1.Models.ProductStatus.可銷售, 299m, "~/images/slipper1.svg");
        db.SaveChanges();

        // 建立五種系統角色與開發用管理帳號。
        var roleManager = services.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<WebApplication1.Models.ApplicationUser>>();

        foreach (var role in WebApplication1.Models.SystemRoles.All)
        {
            if (!roleManager.RoleExistsAsync(role).GetAwaiter().GetResult())
            {
                roleManager.CreateAsync(new IdentityRole(role)).GetAwaiter().GetResult();
            }
        }

        void EnsureUser(string account, string email, string displayName, string password, string role)
        {
            var user = userManager.FindByNameAsync(account).GetAwaiter().GetResult()
                ?? userManager.FindByEmailAsync(email).GetAwaiter().GetResult();
            if (user == null)
            {
                user = new WebApplication1.Models.ApplicationUser
                {
                    UserName = account, Email = email, DisplayName = displayName, CreatedAt = DateTime.UtcNow
                };
                if (!userManager.CreateAsync(user, password).GetAwaiter().GetResult().Succeeded) return;
            }
            else if (!string.Equals(user.UserName, account, StringComparison.OrdinalIgnoreCase)
                && userManager.FindByNameAsync(account).GetAwaiter().GetResult() == null)
            {
                user.UserName = account;
                userManager.UpdateAsync(user).GetAwaiter().GetResult();
            }

            if (!userManager.IsInRoleAsync(user, role).GetAwaiter().GetResult())
                userManager.AddToRoleAsync(user, role).GetAwaiter().GetResult();
        }

        EnsureUser("system", "system@example.com", "System", "System@123", WebApplication1.Models.SystemRoles.System);
        EnsureUser("admin", "admin@example.com", "Admin", "Admin@123", WebApplication1.Models.SystemRoles.Admin);

        // 舊會員若尚無角色，預設補為 Normal。
        foreach (var user in userManager.Users.ToList())
        {
            if (userManager.GetRolesAsync(user).GetAwaiter().GetResult().Count == 0)
                userManager.AddToRoleAsync(user, WebApplication1.Models.SystemRoles.Normal).GetAwaiter().GetResult();
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred seeding the DB.");
    }
}

app.Run();
