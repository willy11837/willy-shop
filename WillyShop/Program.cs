using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting.Compact;
using WillyShop.Data;
using WillyShop.Contracts;
using WillyShop.Filters;
using WillyShop.Logging;
using WillyShop.Middleware;
using WillyShop.Models;
using WillyShop.Options;
using WillyShop.Services.Auth;

var builder = WebApplication.CreateBuilder(args);

SelfLog.Enable(message => Console.Error.WriteLine(message));
builder.Services.AddSerilog((services, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "WillyShop");

    if (builder.Environment.IsDevelopment())
    {
        loggerConfiguration.WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
    }
    else
    {
        loggerConfiguration.WriteTo.Async(
            sinkConfiguration => sinkConfiguration.Console(new RenderedCompactJsonFormatter()),
            bufferSize: 10_000,
            blockWhenFull: false,
            monitor: new AsyncLogSinkMonitor());
    }
});

builder.Services.AddControllers(options => options.Filters.Add<ApiAntiforgeryFilter>());
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage) ? "輸入資料格式不正確。" : error.ErrorMessage)
                    .ToArray());
        return new BadRequestObjectResult(new ApiResponse<ApiValidationErrorData>(
            false,
            StatusCodes.Status400BadRequest,
            string.Join("、", errors.Values.SelectMany(values => values).Distinct()),
            new ApiValidationErrorData(errors)));
    };
});
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

// 中文註解：註冊 ASP.NET Core Identity 作為應用程式的認證/授權系統，並使用 ApplicationDbContext 作為儲存層。
builder.Services.AddIdentityCore<ApplicationUser>(options =>
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
    options.Stores.MaxLengthForKeys = 128;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// 中文提示：注入客製化的 IdentityErrorDescriber 以回傳中文驗證錯誤訊息
builder.Services.AddScoped<Microsoft.AspNetCore.Identity.IdentityErrorDescriber, WillyShop.Models.ChineseIdentityErrorDescriber>();

// 中文註解：啟用記憶式快取與 Session，用於儲存匿名購物車（AnonCart）。
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(4);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// 中文註解：註冊購物車服務（處理匿名購物車與使用者購物車的合併邏輯）。
builder.Services.AddScoped<WillyShop.Services.CartService>();
builder.Services.AddScoped<WillyShop.Services.CartApplicationService>();
builder.Services.AddScoped<WillyShop.Services.OrderApplicationService>();
builder.Services.AddScoped<WillyShop.Services.ProductQueryService>();
builder.Services.AddScoped<WillyShop.Services.NotificationService>();
builder.Services.AddScoped<ITokenService, TokenService>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
builder.Services.Configure<JwtOptions>(jwtSection);
var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.Issuer)
    || string.IsNullOrWhiteSpace(jwtOptions.Audience)
    || Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) < 32)
    throw new InvalidOperationException("Jwt 設定不完整；SigningKey 必須至少 32 bytes。");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            NameClaimType = System.Security.Claims.ClaimTypes.Name,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            ClockSkew = TimeSpan.FromSeconds(jwtOptions.ClockSkewSeconds)
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new ApiResponse<object?>(
                    false,
                    StatusCodes.Status401Unauthorized,
                    "需要有效的登入憑證。",
                    null));
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new ApiResponse<object?>(
                    false,
                    StatusCodes.Status403Forbidden,
                    "目前帳號沒有執行此操作的權限。",
                    null));
            }
        };
    });
builder.Services.AddAuthorization();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    });
});

var app = builder.Build();

app.UseWhen(context => context.Request.Path.StartsWithSegments("/api"), apiApp =>
{
    apiApp.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        options.GetLevel = (httpContext, elapsed, exception) => exception is not null
            || httpContext.Response.StatusCode >= StatusCodes.Status500InternalServerError
                ? LogEventLevel.Error
                : elapsed >= 2_000
                    ? LogEventLevel.Warning
                    : LogEventLevel.Information;
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set(
                "TraceId",
                Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier);
            if (Activity.Current is { } activity)
                diagnosticContext.Set("SpanId", activity.SpanId.ToString());

            var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirstValue("sub");
            if (!string.IsNullOrWhiteSpace(userId))
                diagnosticContext.Set("UserId", userId);

            var endpointName = httpContext.GetEndpoint()?.DisplayName;
            if (!string.IsNullOrWhiteSpace(endpointName))
                diagnosticContext.Set("EndpointName", endpointName);
        };
    });
});

// Configure the HTTP request pipeline.
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        var activity = Activity.Current;
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");
        context.RequestServices.GetRequiredService<ILogger<Program>>()
            .UnhandledRequestException(
                exception ?? new InvalidOperationException("The exception handler did not receive an exception."),
                context.Request.Method,
                context.Request.Path,
                userId,
                activity?.TraceId.ToString() ?? context.TraceIdentifier,
                activity?.SpanId.ToString());
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(new ApiResponse<object?>(
            false,
            StatusCodes.Status500InternalServerError,
            "伺服器發生未預期的錯誤。",
            null));
    });
});
app.UseStatusCodePages(async context =>
{
    var response = context.HttpContext.Response;
    if (!context.HttpContext.Request.Path.StartsWithSegments("/api")
        || response.HasStarted
        || response.ContentLength is > 0)
        return;

    var message = response.StatusCode switch
    {
        StatusCodes.Status400BadRequest => "要求無效",
        StatusCodes.Status401Unauthorized => "需要有效的登入憑證。",
        StatusCodes.Status403Forbidden => "目前帳號沒有執行此操作的權限。",
        StatusCodes.Status404NotFound => "找不到要求的資源。",
        StatusCodes.Status409Conflict => "資料發生衝突。",
        _ => "要求失敗。"
    };
    await response.WriteAsJsonAsync(new ApiResponse<object?>(false, response.StatusCode, message, null));
});
if (!app.Environment.IsDevelopment()) app.UseHsts();

app.UseHttpsRedirection();
var staticFileContentTypeProvider = new FileExtensionContentTypeProvider();
staticFileContentTypeProvider.Mappings[".webp"] = "image/webp";
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = staticFileContentTypeProvider
});
var frontendDistPath = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "frontend", "dist"));
if (Directory.Exists(frontendDistPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(frontendDistPath),
        ContentTypeProvider = staticFileContentTypeProvider
    });
}

app.UseRouting();
app.UseCors();

// 中文註解：啟用 Session 與 Authentication/Authorization 中介軟體。
app.UseSession();
app.UseAuthentication();
app.UseMiddleware<RequestLoggingScopeMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapFallback("/api/{**path}", () => Results.Json(
    new ApiResponse<object?>(false, StatusCodes.Status404NotFound, "要求的 API 路由不存在。", null),
    statusCode: StatusCodes.Status404NotFound));
var publishedSpaIndexPath = Path.Combine(app.Environment.WebRootPath ?? string.Empty, "index.html");
var spaIndexPath = File.Exists(publishedSpaIndexPath)
    ? publishedSpaIndexPath
    : Path.Combine(frontendDistPath, "index.html");
app.MapFallback(async context =>
{
    if (!File.Exists(spaIndexPath))
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new ApiResponse<object?>(
            false,
            StatusCodes.Status503ServiceUnavailable,
            "請先執行 frontend production build。",
            null));
        return;
    }

    context.Response.ContentType = "text/html; charset=utf-8";
    await context.Response.SendFileAsync(spaIndexPath);
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
        void AddProductIfMissing(string code, string name, string category, int qty, WillyShop.Models.ProductStatus status, decimal price, string imagePath)
        {
            var existing = db.Products.FirstOrDefault(p => p.ProductCode == code);
            if (existing == null)
            {
                db.Products.Add(new WillyShop.Models.Product
                {
                    ProductCode = code, Name = name, Category = category, StockQuantity = qty,
                    Status = status, Price = price, ImagePath = imagePath, CreatedAt = DateTime.UtcNow,
                    ModifiedBy = "System", ModifiedAt = DateTime.UtcNow
                });
            }
        }

        AddProductIfMissing("P001", "灰色室內拖鞋", "拖鞋", 10, WillyShop.Models.ProductStatus.可銷售, 199m, "~/images/slipper1.jpg");
        AddProductIfMissing("P002", "黑色室外拖鞋", "拖鞋", 5, WillyShop.Models.ProductStatus.可銷售, 249m, "~/images/slipper2.jpg");
        AddProductIfMissing("P004", "純棉短袖 T 恤", "服飾", 24, WillyShop.Models.ProductStatus.可銷售, 390m, "~/images/product-placeholder.svg");
        AddProductIfMissing("P005", "防風連帽外套", "服飾", 12, WillyShop.Models.ProductStatus.可銷售, 1280m, "~/images/product-placeholder.svg");
        AddProductIfMissing("P006", "都會通勤後背包", "包包", 15, WillyShop.Models.ProductStatus.可銷售, 990m, "~/images/commuter-backpack.webp");
        AddProductIfMissing("P007", "輕量休閒斜背包", "包包", 18, WillyShop.Models.ProductStatus.可銷售, 590m, "~/images/product-placeholder.svg");
        AddProductIfMissing("P008", "不鏽鋼保溫杯", "居家生活", 30, WillyShop.Models.ProductStatus.可銷售, 450m, "~/images/product-placeholder.svg");
        AddProductIfMissing("P009", "木質香氛擴香組", "居家生活", 20, WillyShop.Models.ProductStatus.可銷售, 680m, "~/images/product-placeholder.svg");
        AddProductIfMissing("P010", "20W USB-C 快速充電器", "3C 配件", 25, WillyShop.Models.ProductStatus.可銷售, 490m, "~/images/product-placeholder.svg");
        AddProductIfMissing("P011", "尼龍編織充電線", "3C 配件", 40, WillyShop.Models.ProductStatus.可銷售, 299m, "~/images/product-placeholder.svg");
        AddProductIfMissing("P012", "防滑瑜珈墊", "運動用品", 14, WillyShop.Models.ProductStatus.可銷售, 790m, "~/images/product-placeholder.svg");
        AddProductIfMissing("P013", "戶外運動水壺", "運動用品", 22, WillyShop.Models.ProductStatus.可銷售, 350m, "~/images/product-placeholder.svg");

        // 僅修正仍使用系統預設圖的既有商品，不覆蓋管理員自行設定的圖片。
        var commuterBackpack = db.Products.FirstOrDefault(product => product.ProductCode == "P006");
        if (commuterBackpack != null
            && (string.IsNullOrWhiteSpace(commuterBackpack.ImagePath)
                || commuterBackpack.ImagePath == "~/images/product-placeholder.svg"
                || commuterBackpack.ImagePath == "~/images/sg-11134201-8259z-mt0hdomgmnex96@resize_w900_nl.webp"))
        {
            commuterBackpack.ImagePath = "~/images/commuter-backpack.webp";
            commuterBackpack.ModifiedBy = "System";
            commuterBackpack.ModifiedAt = DateTime.UtcNow;
        }

        // 移除舊版系統建立的 P003 假資料；若後台已改成其他商品則保留。
        var obsoleteSeedProduct = db.Products.FirstOrDefault(product =>
            product.ProductCode == "P003"
            && product.Name == "藍色防滑拖鞋"
            && product.Category == "拖鞋"
            && product.ModifiedBy == "System");
        if (obsoleteSeedProduct != null)
        {
            db.Products.Remove(obsoleteSeedProduct);
        }

        db.SaveChanges();

        // 建立五種系統角色與開發用管理帳號。
        var roleManager = services.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<WillyShop.Models.ApplicationUser>>();

        foreach (var role in WillyShop.Models.SystemRoles.All)
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
                user = new WillyShop.Models.ApplicationUser
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

        var systemPassword = builder.Configuration["SeedUsers:SystemPassword"];
        if (!string.IsNullOrWhiteSpace(systemPassword))
        {
            EnsureUser("system", "system@example.com", "System", systemPassword, WillyShop.Models.SystemRoles.System);
        }

        var adminPassword = builder.Configuration["SeedUsers:AdminPassword"];
        if (!string.IsNullOrWhiteSpace(adminPassword))
        {
            EnsureUser("admin", "admin@example.com", "Admin", adminPassword, WillyShop.Models.SystemRoles.Admin);
        }

        // 舊會員若尚無角色，預設補為 Normal。
        foreach (var user in userManager.Users.ToList())
        {
            if (userManager.GetRolesAsync(user).GetAwaiter().GetResult().Count == 0)
                userManager.AddToRoleAsync(user, WillyShop.Models.SystemRoles.Normal).GetAwaiter().GetResult();
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.DatabaseInitializationFailed(ex);
    }
}

app.Run();

public partial class Program;
