# TMS authentication + first web interface installer. Run next to TradingManagementSystem.slnx.
# Review this script before running. Changes to existing source files are backed up in TEMP.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if (-not (Test-Path (Join-Path $root 'TradingManagementSystem.slnx'))) { throw 'Move this .ps1 next to TradingManagementSystem.slnx, then run it again.' }
$web = Join-Path $root 'TradingManagementSystem.Web'
$program = Join-Path $web 'Program.cs'
$api = Join-Path $web 'Controllers\Api'
$required = @('SalesApiController.cs','InventoryApiController.cs','ProductsApiController.cs','SuppliersApiController.cs','TradingPointsApiController.cs','PurchasesApiController.cs','ReceiptsApiController.cs')
foreach ($name in $required) { if (-not (Test-Path (Join-Path $api $name))) { throw "Missing $name. Keep your latest Sales/Inventory changes and run again." } }
if ((Get-Content -LiteralPath $program -Raw) -match 'TMS.Auth') { throw 'This package appears already installed. Nothing changed.' }
$file0 = @'
using System.ComponentModel.DataAnnotations;

namespace TradingManagementSystem.Web.Models;

public class LoginInput
{
    [Required(ErrorMessage = "Введи логін")]
    public string Login { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введи пароль")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

public class EmployeeInput
{
    [Required, StringLength(60)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(60)]
    public string LastName { get; set; } = string.Empty;

    [Required, RegularExpression("^[a-zA-Z0-9._-]{3,32}$", ErrorMessage = "Логін: 3–32 символи, латинські букви, цифри, крапка, _ або -")]
    public string Login { get; set; } = string.Empty;

    [Required, MinLength(10, ErrorMessage = "Пароль повинен містити щонайменше 10 символів")]
    [StringLength(128)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public TradingManagementSystem.Domain.Enums.EmployeeRole Role { get; set; }
}

public class PasswordChangeInput
{
    [Required, DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, MinLength(10), StringLength(128), DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;
}
'@
$file1 = @'
using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TradingManagementSystem.Domain.Entities;
using TradingManagementSystem.Domain.Enums;
using TradingManagementSystem.Infrastructure.Data;
using TradingManagementSystem.Web.Models;

namespace TradingManagementSystem.Web.Controllers;

// First-run setup is ONLY available locally in Development and only while no employees exist.
[AllowAnonymous]
public class SetupController : Controller
{
    private readonly MongoDbContext _db;
    private readonly IWebHostEnvironment _environment;

    public SetupController(MongoDbContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    private bool LocalDevelopment =>
        _environment.IsDevelopment() &&
        HttpContext.Connection.RemoteIpAddress is { } ip &&
        IPAddress.IsLoopback(ip);

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!LocalDevelopment) return NotFound();
        if (await _db.Employees.Find(_ => true).AnyAsync())
            return RedirectToAction("Login", "Account");
        return View(new EmployeeInput { Role = EmployeeRole.Director });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(EmployeeInput model)
    {
        if (!LocalDevelopment) return NotFound();
        if (await _db.Employees.Find(_ => true).AnyAsync())
            return Conflict("Перший користувач вже існує. Початкове налаштування завершено.");
        model.Role = EmployeeRole.Director; // Never accept a role from this form.
        if (!ModelState.IsValid) return View(model);
        var login = model.Login.Trim().ToLowerInvariant();
        await _db.Employees.Indexes.CreateOneAsync(
            new CreateIndexModel<Employee>(Builders<Employee>.IndexKeys.Ascending(x => x.Login),
                new CreateIndexOptions { Unique = true }));
        var person = new Employee
        {
            Id = Guid.NewGuid().ToString(),
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim(),
            Login = login,
            Role = EmployeeRole.Director
        };
        person.ChangePassword(new PasswordHasher<Employee>().HashPassword(person, model.Password));
        try { await _db.Employees.InsertOneAsync(person); }
        catch (MongoWriteException) { return Conflict("Не вдалося створити першого користувача. Перевір базу."); }
        TempData["Flash"] = "Директора створено. Увійди зі своїм логіном і паролем.";
        return RedirectToAction("Login", "Account");
    }
}
'@
$file2 = @'
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TradingManagementSystem.Domain.Entities;
using TradingManagementSystem.Infrastructure.Data;
using TradingManagementSystem.Web.Models;

namespace TradingManagementSystem.Web.Controllers;

public class AccountController : Controller
{
    private readonly MongoDbContext _db;

    public AccountController(MongoDbContext db) => _db = db;

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");
        ViewData["ReturnUrl"] = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        return View(new LoginInput());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginInput model, string? returnUrl = null)
    {
        if (!ModelState.IsValid) return View(model);
        var login = model.Login.Trim().ToLowerInvariant();
        var user = await _db.Employees.Find(x => x.Login == login && x.IsActive)
            .FirstOrDefaultAsync();
        var ok = user != null && new PasswordHasher<Employee>()
            .VerifyHashedPassword(user, user.PasswordHash, model.Password)
            != PasswordVerificationResult.Failed;
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, "Невірний логін або пароль");
            return View(model);
        }
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user!.Id),
            new Claim(ClaimTypes.Name, user.FirstName + " " + user.LastName),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties { IsPersistent = false });
        if (Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl!);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult ChangePassword() => View(new PasswordChangeInput());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(PasswordChangeInput model)
    {
        if (!ModelState.IsValid) return View(model);
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _db.Employees.Find(x => x.Id == id && x.IsActive).FirstOrDefaultAsync();
        if (user == null) return Unauthorized();
        var hasher = new PasswordHasher<Employee>();
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, model.CurrentPassword) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "Поточний пароль неправильний");
            return View(model);
        }
        user.ChangePassword(hasher.HashPassword(user, model.NewPassword));
        await _db.Employees.ReplaceOneAsync(x => x.Id == user.Id, user);
        TempData["Flash"] = "Пароль змінено.";
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();
}
'@
$file3 = @'
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TradingManagementSystem.Domain.Entities;
using TradingManagementSystem.Domain.Enums;
using TradingManagementSystem.Infrastructure.Data;
using TradingManagementSystem.Web.Models;

namespace TradingManagementSystem.Web.Controllers;

[Authorize(Roles = nameof(EmployeeRole.Director))]
public class EmployeesController : Controller
{
    private readonly MongoDbContext _db;
    public EmployeesController(MongoDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index() =>
        View(await _db.Employees.Find(_ => true).ToListAsync());

    [HttpGet]
    public IActionResult Create() => View(new EmployeeInput { Role = EmployeeRole.Seller });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EmployeeInput model)
    {
        if (!Enum.IsDefined(model.Role))
            ModelState.AddModelError(nameof(model.Role), "Неправильна роль");
        if (!ModelState.IsValid) return View(model);
        var login = model.Login.Trim().ToLowerInvariant();
        if (await _db.Employees.Find(x => x.Login == login).AnyAsync())
        {
            ModelState.AddModelError(nameof(model.Login), "Такий логін уже існує");
            return View(model);
        }
        var employee = new Employee
        {
            Id = Guid.NewGuid().ToString(),
            Login = login,
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim(),
            Role = model.Role
        };
        employee.ChangePassword(new PasswordHasher<Employee>().HashPassword(employee, model.Password));
        try { await _db.Employees.InsertOneAsync(employee); }
        catch (MongoWriteException)
        {
            ModelState.AddModelError(nameof(model.Login), "Логін уже використовується");
            return View(model);
        }
        TempData["Flash"] = "Працівника створено.";
        return RedirectToAction(nameof(Index));
    }
}
'@
$file4 = @'
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TradingManagementSystem.Domain.Entities;
using TradingManagementSystem.Infrastructure.Data;

namespace TradingManagementSystem.Web.Controllers;

public class CatalogController : Controller
{
    private readonly MongoDbContext _db;
    public CatalogController(MongoDbContext db) => _db = db;

    public async Task<IActionResult> Products() =>
        View(await _db.Products.Find(_ => true).ToListAsync());

    [Authorize(Roles = "Director,SupplyManager")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddProduct(string name, string sku, string category, decimal sellingPrice, string description)
    {
        if (string.IsNullOrWhiteSpace(sku) || string.IsNullOrWhiteSpace(name) || sellingPrice < 0)
        {
            TempData["Flash"] = "Перевір назву, SKU та ціну товару.";
            return RedirectToAction(nameof(Products));
        }
        if (await _db.Products.Find(x => x.Sku == sku.Trim()).AnyAsync())
        {
            TempData["Flash"] = "SKU вже використовується.";
            return RedirectToAction(nameof(Products));
        }
        var product = new Product { Id = Guid.NewGuid().ToString(), Sku = sku.Trim() };
        product.UpdateData(name, category, sellingPrice, description);
        await _db.Products.InsertOneAsync(product);
        TempData["Flash"] = "Товар створено.";
        return RedirectToAction(nameof(Products));
    }

    [Authorize(Roles = "Director,SupplyManager,TradingPointManager")]
    public async Task<IActionResult> Suppliers() =>
        View(await _db.Suppliers.Find(_ => true).ToListAsync());

    [Authorize(Roles = "Director,SupplyManager")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSupplier(string name, string phone, string email, string address)
    {
        var supplier = new Supplier { Id = Guid.NewGuid().ToString() };
        try { supplier.UpdateData(name, phone, email, address); }
        catch (ArgumentException ex)
        {
            TempData["Flash"] = ex.Message;
            return RedirectToAction(nameof(Suppliers));
        }
        await _db.Suppliers.InsertOneAsync(supplier);
        TempData["Flash"] = "Постачальника створено.";
        return RedirectToAction(nameof(Suppliers));
    }

    public async Task<IActionResult> TradingPoints() =>
        View(await _db.TradingPoints.Find(_ => true).ToListAsync());

    [Authorize(Roles = "Director")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTradingPoint(string name, string address)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(address))
        {
            TempData["Flash"] = "Заповни назву й адресу.";
            return RedirectToAction(nameof(TradingPoints));
        }
        await _db.TradingPoints.InsertOneAsync(new TradingPoint
        {
            Id = Guid.NewGuid().ToString(), Name = name.Trim(), Address = address.Trim()
        });
        TempData["Flash"] = "Торгову точку створено.";
        return RedirectToAction(nameof(TradingPoints));
    }

    [Authorize(Roles = "Director,SupplyManager")]
    public async Task<IActionResult> Purchases() =>
        View(await _db.Purchases.Find(_ => true).ToListAsync());

    [Authorize(Roles = "Director,SupplyManager")]
    public async Task<IActionResult> Receipts() =>
        View(await _db.Receipts.Find(_ => true).ToListAsync());

    [Authorize(Roles = "Director,TradingPointManager,Seller")]
    public async Task<IActionResult> Sales()
    {
        var sellerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var filter = User.IsInRole("Seller")
            ? Builders<Sale>.Filter.Eq(x => x.SellerId, sellerId)
            : Builders<Sale>.Filter.Empty;
        return View(await _db.Sales.Find(filter).ToListAsync());
    }

    public async Task<IActionResult> Inventory() =>
        View(await _db.InventoryItems.Find(_ => true).ToListAsync());
}
'@
$file5 = @'
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TradingManagementSystem.Domain.Entities;
using TradingManagementSystem.Infrastructure.Data;

namespace TradingManagementSystem.Web.Controllers;

public class HomeController : Controller
{
    private readonly MongoDbContext _db;
    public HomeController(MongoDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var inventory = await _db.InventoryItems.Find(_ => true).ToListAsync();
        var model = new DashboardStats
        {
            Products = await _db.Products.CountDocumentsAsync(Builders<Product>.Filter.Empty),
            Suppliers = await _db.Suppliers.CountDocumentsAsync(Builders<Supplier>.Filter.Empty),
            TradingPoints = await _db.TradingPoints.CountDocumentsAsync(Builders<TradingPoint>.Filter.Empty),
            Purchases = await _db.Purchases.CountDocumentsAsync(Builders<Purchase>.Filter.Empty),
            Sales = await _db.Sales.CountDocumentsAsync(Builders<Sale>.Filter.Empty),
            LowStock = inventory.Count(i => i.Quantity <= i.MinimumQuantity)
        };
        return View(model);
    }

    public IActionResult Privacy() => RedirectToAction(nameof(Index));
    public IActionResult Error() => View(new TradingManagementSystem.Web.Models.ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
}

public class DashboardStats
{
    public long Products { get; set; }
    public long Suppliers { get; set; }
    public long TradingPoints { get; set; }
    public long Purchases { get; set; }
    public long Sales { get; set; }
    public int LowStock { get; set; }
}
'@
$file6 = @'
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using MongoDB.Driver;
using System.Security.Claims;
using TradingManagementSystem.Application.Services;
using TradingManagementSystem.Domain.Entities;
using TradingManagementSystem.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Every MVC page and API action requires sign-in unless [AllowAnonymous] is present.
builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "TMS.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        // A disabled user or a changed role must not keep an old cookie.
        options.Events.OnValidatePrincipal = async ctx =>
        {
            var userId = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) { ctx.RejectPrincipal(); return; }
            var db = ctx.HttpContext.RequestServices.GetRequiredService<MongoDbContext>();
            var current = await db.Employees.Find(x => x.Id == userId && x.IsActive).FirstOrDefaultAsync();
            if (current == null || current.Role.ToString() != ctx.Principal?.FindFirstValue(ClaimTypes.Role))
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(options => options.Cookie.SecurePolicy = CookieSecurePolicy.Always);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var mongoConnectionString = builder.Configuration["MongoDb:ConnectionString"]
    ?? throw new InvalidOperationException("MongoDB connection string is missing.");
var mongoDatabaseName = builder.Configuration["MongoDb:DatabaseName"]
    ?? throw new InvalidOperationException("MongoDB database name is missing.");
builder.Services.AddSingleton(new MongoDbContext(mongoConnectionString, mongoDatabaseName));
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<ReceiptService>();
builder.Services.AddScoped<SaleService>();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();
'@
$file7 = @'
@using System.Security.Claims
<!DOCTYPE html>
<html lang="uk">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>@ViewData["Title"] · Trading Management System</title>
    <link rel="stylesheet" href="~/lib/bootstrap/dist/css/bootstrap.min.css" />
    <link rel="stylesheet" href="~/css/tms.css" asp-append-version="true" />
</head>
<body>
    @if (User.Identity?.IsAuthenticated == true)
    {
        <div class="app-shell">
            <aside class="sidebar">
                <div class="brand"><span class="brand-icon">T</span><span>Trading<br/><small>Management System</small></span></div>
                <nav class="side-nav" aria-label="Основна навігація">
                    <a asp-controller="Home" asp-action="Index">◫ <span>Головна</span></a>
                    <a asp-controller="Catalog" asp-action="Products">▦ <span>Товари</span></a>
                    @if (!User.IsInRole("Seller")) { <a asp-controller="Catalog" asp-action="Suppliers">◇ <span>Постачальники</span></a> }
                    <a asp-controller="Catalog" asp-action="TradingPoints">⌂ <span>Торгові точки</span></a>
                    @if (User.IsInRole("Director") || User.IsInRole("SupplyManager"))
                    {
                        <a asp-controller="Catalog" asp-action="Purchases">▤ <span>Закупівлі</span></a>
                        <a asp-controller="Catalog" asp-action="Receipts">↧ <span>Надходження</span></a>
                    }
                    @if (User.IsInRole("Director") || User.IsInRole("TradingPointManager") || User.IsInRole("Seller"))
                    { <a asp-controller="Catalog" asp-action="Sales">◈ <span>Продажі</span></a> }
                    <a asp-controller="Catalog" asp-action="Inventory">▥ <span>Залишки</span></a>
                    @if (User.IsInRole("Director"))
                    { <a asp-controller="Employees" asp-action="Index">♙ <span>Працівники</span></a> }
                </nav>
                <div class="side-bottom"><a asp-controller="Account" asp-action="ChangePassword">⚙ Змінити пароль</a></div>
            </aside>
            <div class="workspace">
                <header class="topbar">
                    <div class="topbar-title">Trading Management System</div>
                    <div class="profile"><span>@User.Identity.Name</span><span class="role-pill">@User.FindFirstValue(ClaimTypes.Role)</span>
                        <form asp-controller="Account" asp-action="Logout" method="post"><button type="submit" class="logout-btn">Вийти</button></form>
                    </div>
                </header>
                <main class="page-content">
                    @if (TempData["Flash"] is string notice) { <div class="flash">@notice</div> }
                    @RenderBody()
                </main>
            </div>
        </div>
    }
    else
    {
        <main class="auth-main">@RenderBody()</main>
    }
    <script src="~/lib/bootstrap/dist/js/bootstrap.bundle.min.js"></script>
    @await RenderSectionAsync("Scripts", required: false)
</body>
</html>
'@
$file8 = @'
@model TradingManagementSystem.Web.Controllers.DashboardStats
@{
    ViewData["Title"] = "Головна";
}
<div class="page-heading"><div><span class="eyebrow">ОГЛЯД СИСТЕМИ</span><h1>Панель керування</h1><p>Актуальні показники торгової організації</p></div><span class="small-chip">Поточні дані MongoDB</span></div>
<div class="stats-grid">
    <a class="stat-card" asp-controller="Catalog" asp-action="Products"><span>Товари</span><strong>@Model.Products</strong><small>Каталог продукції</small></a>
    <a class="stat-card" asp-controller="Catalog" asp-action="TradingPoints"><span>Торгові точки</span><strong>@Model.TradingPoints</strong><small>Філії та магазини</small></a>
    <a class="stat-card" asp-controller="Catalog" asp-action="Inventory"><span>Низькі залишки</span><strong class="accent-number">@Model.LowStock</strong><small>Позиції для уваги</small></a>
    <a class="stat-card" asp-controller="Catalog" asp-action="Inventory"><span>Продажі</span><strong>@Model.Sales</strong><small>Проведені операції</small></a>
</div>
<div class="panel-grid">
    <section class="content-card"><span class="eyebrow">ПРОЦЕС</span><h2>Робота з товарами</h2><p>Створення закупівлі не змінює запас. Надходження збільшує кількість, а продаж зменшує залишок.</p><div class="flowline"><span>Закупівля</span><b>→</b><span>Надходження</span><b>→</b><span>Продаж</span></div></section>
    <section class="content-card"><span class="eyebrow">СТАН ПРОЄКТУ</span><h2>Функціональні модулі</h2><p>Додано облік товарів, постачальників, торгових точок, надходжень та продажів.</p><p class="note">Створення закупівель і продажів через окремі вебформи та Smart Inventory — наступні кроки. Поки доступні через API.</p><a class="text-link" href="/swagger">Відкрити Swagger →</a></section>
</div>
'@
$file9 = @'
@model TradingManagementSystem.Web.Models.LoginInput
@{ ViewData["Title"] = "Вхід"; }
<div class="auth-card">
    <div class="auth-logo">T</div>
    <span class="eyebrow">TRADING MANAGEMENT SYSTEM</span>
    <h1>Вхід у систему</h1><p>Увійди під обліковим записом працівника.</p>
    @if (TempData["Flash"] is string message) { <div class="flash">@message</div> }
    <form method="post" asp-action="Login" asp-route-returnUrl="@ViewData["ReturnUrl"]">
        <div asp-validation-summary="All" class="validation-summary"></div>
        <label asp-for="Login">Логін</label><input asp-for="Login" autocomplete="username" class="form-control" />
        <label asp-for="Password">Пароль</label><input asp-for="Password" type="password" autocomplete="current-password" class="form-control" />
        <button class="primary-btn w-100 mt-3" type="submit">Увійти</button>
    </form>
    <div class="auth-hint">Перший запуск? <a asp-controller="Setup" asp-action="Index">Створити директора</a> (лише локально).</div>
</div>
'@
$file10 = @'
@{ ViewData["Title"] = "Немає доступу"; }
<section class="content-card narrow"><h1>Немає доступу</h1><p>Твоя роль не дозволяє виконувати цю операцію.</p><a asp-controller="Home" asp-action="Index" class="text-link">Повернутися на головну →</a></section>
'@
$file11 = @'
@model TradingManagementSystem.Web.Models.PasswordChangeInput
@{ ViewData["Title"] = "Зміна пароля"; }
<div class="page-heading"><div><span class="eyebrow">ОБЛІКОВИЙ ЗАПИС</span><h1>Зміна пароля</h1></div></div>
<section class="content-card narrow"><form method="post" asp-action="ChangePassword"><div asp-validation-summary="All" class="validation-summary"></div><label>Поточний пароль</label><input asp-for="CurrentPassword" type="password" class="form-control" required/><label>Новий пароль (від 10 символів)</label><input asp-for="NewPassword" type="password" class="form-control" minlength="10" required/><button class="primary-btn mt-3" type="submit">Зберегти новий пароль</button></form></section>
'@
$file12 = @'
@model TradingManagementSystem.Web.Models.EmployeeInput
@{ ViewData["Title"] = "Перший запуск"; }
<div class="auth-card">
    <div class="auth-logo">T</div><span class="eyebrow">ПЕРШИЙ ЗАПУСК</span>
    <h1>Створити директора</h1>
    <p>Створи власний логін і пароль. Ця форма доступна лише локально в режимі розробки, поки в базі немає працівників.</p>
    <form method="post" asp-action="Index"><div asp-validation-summary="All" class="validation-summary"></div>
        <label>Ім'я</label><input asp-for="FirstName" class="form-control" required />
        <label>Прізвище</label><input asp-for="LastName" class="form-control" required />
        <label>Логін (латиниця)</label><input asp-for="Login" class="form-control" required />
        <label>Пароль (від 10 символів)</label><input asp-for="Password" type="password" class="form-control" minlength="10" autocomplete="new-password" required />
        <button type="submit" class="primary-btn w-100 mt-3">Створити директора</button>
    </form>
</div>
'@
$file13 = @'
@model List<TradingManagementSystem.Domain.Entities.Employee>
@{ ViewData["Title"] = "Працівники"; }
<div class="page-heading"><div><span class="eyebrow">КЕРУВАННЯ ДОСТУПОМ</span><h1>Працівники</h1><p>Облікові записи та ролі користувачів</p></div><a asp-action="Create" class="primary-btn">+ Додати працівника</a></div>
<div class="table-card"><table class="data-table"><thead><tr><th>Працівник</th><th>Логін</th><th>Роль</th><th>Стан</th></tr></thead><tbody>
@foreach(var item in Model) { <tr><td>@item.FirstName @item.LastName</td><td>@item.Login</td><td>@item.Role</td><td>@(item.IsActive ? "Активний" : "Вимкнений")</td></tr> }
</tbody></table></div>
'@
$file14 = @'
@model TradingManagementSystem.Web.Models.EmployeeInput
@using TradingManagementSystem.Domain.Enums
@{ ViewData["Title"] = "Новий працівник"; }
<div class="page-heading"><div><span class="eyebrow">ПРАЦІВНИКИ</span><h1>Додати працівника</h1></div></div>
<div class="content-card narrow"><form method="post" asp-action="Create"><div asp-validation-summary="All" class="validation-summary"></div>
<label>Ім'я</label><input asp-for="FirstName" class="form-control" required />
<label>Прізвище</label><input asp-for="LastName" class="form-control" required />
<label>Логін (латиниця)</label><input asp-for="Login" class="form-control" required />
<label>Пароль (від 10 символів)</label><input asp-for="Password" type="password" class="form-control" minlength="10" autocomplete="new-password" required />
<label>Роль</label><select asp-for="Role" asp-items="Html.GetEnumSelectList<EmployeeRole>()" class="form-select"></select>
<button class="primary-btn mt-3" type="submit">Створити працівника</button>
</form></div>
'@
$file15 = @'
@model List<TradingManagementSystem.Domain.Entities.Product>
@{ ViewData["Title"] = "Товари"; }
<div class="page-heading"><div><span class="eyebrow">КАТАЛОГ</span><h1>Товари</h1><p>Усього записів: @Model.Count</p></div></div>
@if(User.IsInRole("Director") || User.IsInRole("SupplyManager"))
{
    <details class="content-card form-details"><summary>+ Додати товар</summary>
    <form asp-action="AddProduct" method="post" class="form-grid"><input class="form-control" name="name" placeholder="Назва" required /><input class="form-control" name="sku" placeholder="SKU" required /><input class="form-control" name="category" placeholder="Категорія" /><input class="form-control" name="sellingPrice" type="number" step="0.01" min="0" placeholder="Ціна" required /><input class="form-control full" name="description" placeholder="Опис" /><button class="primary-btn" type="submit">Зберегти товар</button></form></details>
}
<div class="table-card"><table class="data-table"><thead><tr><th>Назва</th><th>SKU</th><th>Категорія</th><th>Ціна</th></tr></thead><tbody>
@foreach(var p in Model) { <tr><td><strong>@p.Name</strong></td><td>@p.Sku</td><td>@p.Category</td><td>@p.SellingPrice</td></tr> }
</tbody></table></div>
'@
$file16 = @'
@model List<TradingManagementSystem.Domain.Entities.Supplier>
@{ ViewData["Title"] = "Постачальники"; }
<div class="page-heading"><div><span class="eyebrow">КАТАЛОГ</span><h1>Постачальники</h1><p>Усього записів: @Model.Count</p></div></div>
@if (User.IsInRole("Director") || User.IsInRole("SupplyManager"))
{
<details class="content-card form-details"><summary>+ Додати постачальника</summary><form asp-action="AddSupplier" method="post" class="form-grid"><input class="form-control" name="name" required placeholder="Назва" /><input class="form-control" name="phone" placeholder="Телефон" /><input class="form-control" name="email" type="email" placeholder="Email" /><input class="form-control" name="address" placeholder="Адреса" /><button class="primary-btn" type="submit">Зберегти</button></form></details>
}
<div class="table-card"><table class="data-table"><thead><tr><th>Назва</th><th>Телефон</th><th>Email</th><th>Адреса</th></tr></thead><tbody>@foreach(var s in Model) { <tr><td><strong>@s.Name</strong></td><td>@s.Phone</td><td>@s.Email</td><td>@s.Address</td></tr> }</tbody></table></div>
'@
$file17 = @'
@model List<TradingManagementSystem.Domain.Entities.TradingPoint>
@{ ViewData["Title"] = "Торгові точки"; }
<div class="page-heading"><div><span class="eyebrow">ОРГАНІЗАЦІЯ</span><h1>Торгові точки</h1><p>Усього записів: @Model.Count</p></div></div>
@if (User.IsInRole("Director"))
{
<details class="content-card form-details"><summary>+ Додати торгову точку</summary><form asp-action="AddTradingPoint" method="post" class="form-grid"><input class="form-control" name="name" placeholder="Назва" required /><input class="form-control" name="address" placeholder="Адреса" required /><button class="primary-btn" type="submit">Зберегти</button></form></details>
}
<div class="table-card"><table class="data-table"><thead><tr><th>Назва</th><th>Адреса</th></tr></thead><tbody>@foreach(var p in Model) { <tr><td><strong>@p.Name</strong></td><td>@p.Address</td></tr> }</tbody></table></div>
'@
$file18 = @'
@model List<TradingManagementSystem.Domain.Entities.Purchase>
@{ ViewData["Title"] = "Закупівлі"; }
<div class="page-heading"><div><span class="eyebrow">ЗАКУПІВЛІ</span><h1>Замовлення постачальникам</h1><p>Перегляд існуючих закупівель</p></div><a class="subtle-btn" href="/swagger">Створити через API →</a></div>
<div class="table-card"><table class="data-table"><thead><tr><th>ID</th><th>Дата</th><th>Статус</th><th>Позицій</th><th>Сума</th></tr></thead><tbody>@foreach(var p in Model) { <tr><td class="mono">@p.Id</td><td>@p.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy")</td><td>@p.Status</td><td>@p.Items.Count</td><td>@p.TotalAmount</td></tr> }</tbody></table></div>
'@
$file19 = @'
@model List<TradingManagementSystem.Domain.Entities.Receipt>
@{ ViewData["Title"] = "Надходження"; }
<div class="page-heading"><div><span class="eyebrow">СКЛАД</span><h1>Надходження товарів</h1><p>Зареєстровані фактичні надходження</p></div><a class="subtle-btn" href="/swagger">Реєструвати через API →</a></div>
<div class="table-card"><table class="data-table"><thead><tr><th>ID</th><th>Закупівля</th><th>Дата</th><th>Зареєстровано</th></tr></thead><tbody>@foreach(var r in Model) { <tr><td class="mono">@r.Id</td><td class="mono">@r.PurchaseId</td><td>@r.ReceivedAt.ToLocalTime().ToString("dd.MM.yyyy")</td><td>@(r.IsRegistered ? "Так" : "Ні")</td></tr> }</tbody></table></div>
'@
$file20 = @'
@model List<TradingManagementSystem.Domain.Entities.Sale>
@{ ViewData["Title"] = "Продажі"; }
<div class="page-heading"><div><span class="eyebrow">ПРОДАЖІ</span><h1>Історія продажів</h1><p>Проведені торгові операції</p></div><a class="subtle-btn" href="/swagger">Продаж через API →</a></div>
<div class="table-card"><table class="data-table"><thead><tr><th>ID</th><th>Дата</th><th>Продавець</th><th>Позицій</th><th>Сума</th></tr></thead><tbody>@foreach(var s in Model) { <tr><td class="mono">@s.Id</td><td>@s.CreatedAt.ToLocalTime().ToString("dd.MM.yyyy")</td><td>@s.SellerId</td><td>@s.Items.Count</td><td>@s.TotalAmount</td></tr> }</tbody></table></div>
'@
$file21 = @'
@model List<TradingManagementSystem.Domain.Entities.InventoryItem>
@{ ViewData["Title"] = "Залишки"; }
<div class="page-heading"><div><span class="eyebrow">СКЛАД</span><h1>Товарні залишки</h1><p>Кількість товару в торгових точках</p></div></div>
<div class="table-card"><table class="data-table"><thead><tr><th>Товар (ID)</th><th>Торгова точка (ID)</th><th>Кількість</th><th>Мінімум</th><th>Стан</th></tr></thead><tbody>@foreach(var i in Model) { <tr><td class="mono">@i.ProductId</td><td class="mono">@i.TradingPointId</td><td><strong>@i.Quantity</strong></td><td>@i.MinimumQuantity</td><td><span class="@(i.Quantity <= i.MinimumQuantity ? "warning-pill" : "ok-pill")">@(i.Quantity <= i.MinimumQuantity ? "Низький" : "Достатньо")</span></td></tr> }</tbody></table></div>
'@
$file22 = @'
:root { --bg: #f4f7fb; --white: #fff; --ink:#182637; --muted:#71829a; --line:#e7ecf4; --blue:#2463eb; --nav:#14243c; }
* { box-sizing:border-box; }
body { margin:0; font-family:Inter,Segoe UI,Arial,sans-serif; color:var(--ink); background:var(--bg); font-size:14px; }
a { text-decoration:none; } a:hover { text-decoration:none; }
.app-shell { min-height:100vh; display:flex; }
.sidebar { width:242px; min-width:242px; background:#15263f; color:#d4dded; display:flex; flex-direction:column; padding:28px 14px 18px; }
.brand { display:flex; gap:12px; align-items:center; margin:3px 12px 38px; line-height:1.14; font-weight:700; font-size:17px; color:white; }
.brand small { font-weight:500; font-size:10px; color:#9eb1d2; letter-spacing:.2px; }
.brand-icon,.auth-logo { display:grid; place-items:center; border-radius:12px; background:#3670fa; color:#fff; font-weight:800; }
.brand-icon { width:38px; height:38px; font-size:22px; }
.side-nav { display:flex; flex-direction:column; gap:4px; }
.side-nav a,.side-bottom a { display:flex; gap:12px; align-items:center; padding:12px 16px; border-radius:10px; color:#c4d0e5; font-size:13px; }
.side-nav a:hover,.side-bottom a:hover { background:#263b59; color:white; }
.side-nav a:first-child { background:#2b4a75; color:white; }
.side-bottom { margin-top:auto; }
.workspace { min-width:0; flex:1; }
.topbar { height:75px; background:#fff; border-bottom:1px solid var(--line); display:flex; justify-content:space-between; align-items:center; padding:0 34px; gap:20px; }
.topbar-title { font-size:14px; font-weight:700; letter-spacing:-.2px; }
.profile { display:flex; align-items:center; gap:15px; font-size:12px; }
.role-pill,.small-chip { border-radius:99px; padding:7px 11px; background:#edf3ff; color:#3261b9; font-size:11px; }
.logout-btn { border:1px solid var(--line); border-radius:8px; background:#fff; color:#44566d; padding:7px 13px; }
.page-content { padding:32px 36px; max-width:1440px; margin:0 auto; }
.page-heading { display:flex; justify-content:space-between; gap:20px; align-items:center; margin:0 0 27px; }
.page-heading h1,.auth-card h1 { font-size:29px; font-weight:800; letter-spacing:-1px; margin:9px 0 7px; }
.page-heading p,.auth-card p,.content-card p { color:var(--muted); margin:0; line-height:1.7; }
.eyebrow { font-size:10px; letter-spacing:1.9px; font-weight:800; color:#5080cb; }
.stats-grid { display:grid; grid-template-columns:repeat(4,minmax(140px,1fr)); gap:17px; }
.stat-card { background:#fff; padding:24px; border-radius:15px; border:1px solid var(--line); display:flex; flex-direction:column; min-height:163px; color:var(--ink); box-shadow:0 3px 16px #24406d06; }
.stat-card:hover { color:var(--ink); border-color:#a9c3ff; }
.stat-card>span { color:#62748f; font-size:12px; font-weight:600; }
.stat-card strong { margin:12px 0 8px; font-size:36px; letter-spacing:-1.5px; }
.stat-card small { color:#91a1b5; }
.accent-number { color:#e0923c; }
.panel-grid { margin-top:20px; display:grid; gap:20px; grid-template-columns:1.2fr 1fr; }
.content-card,.table-card { background:var(--white); border:1px solid var(--line); border-radius:15px; padding:25px; box-shadow:0 3px 16px #24406d06; margin-bottom:18px; }
.content-card h2 { font-size:18px; font-weight:750; margin:10px 0; }
.flowline { display:flex; align-items:center; gap:12px; margin-top:24px; flex-wrap:wrap; }
.flowline span { border-radius:8px; padding:10px 12px; background:#eaf1ff; color:#3f67ae; font-size:12px; }
.flowline b { color:#a5b6cc; }.note { margin-top:15px !important; font-size:12px; }
.text-link { color:#2866d3; font-weight:600; font-size:12px; display:inline-block; margin-top:14px; }
.table-card { overflow-x:auto; padding:5px 20px; }
.data-table { width:100%; border-collapse:collapse; min-width:600px; }
.data-table th,.data-table td { text-align:left; padding:17px 13px; border-bottom:1px solid #eef1f6; }
.data-table th { text-transform:uppercase; letter-spacing:.8px; color:#8a9aab; font-size:10px; }
.data-table td { color:#42526a; font-size:12px; }
.data-table tr:last-child td { border-bottom:0; }.mono { font-family:Consolas,monospace; font-size:10px !important; }
.primary-btn,.subtle-btn { display:inline-flex; align-items:center; justify-content:center; border-radius:9px; border:0; padding:12px 17px; font-weight:650; font-size:12px; }
.primary-btn { background:#316ef1; color:white; }.primary-btn:hover { background:#2158c5; color:white; }
.subtle-btn { background:#e8f0ff; color:#2457ac; }.subtle-btn:hover { background:#d8e6ff; }
.form-details summary { cursor:pointer; font-weight:700; color:#325aa3; }.form-details[open] summary { margin-bottom:18px; }
.form-grid { display:grid; grid-template-columns:repeat(2,minmax(180px,1fr)); gap:12px; }.form-grid .full { grid-column:span 2; }
.auth-main { min-height:100vh; display:flex; justify-content:center; align-items:center; padding:28px; background:radial-gradient(circle at top left,#e8f1ff,#f5f8fc 55%); }
.auth-card { width:100%; max-width:435px; border:1px solid var(--line); background:white; border-radius:20px; padding:39px; box-shadow:0 22px 70px #2f4d7717; }
.auth-logo { height:46px; width:46px; margin-bottom:25px; font-size:26px; }.auth-card h1 { font-size:26px; }
.auth-card p { margin-bottom:22px; }
label { display:block; font-size:12px; font-weight:650; margin:15px 0 7px; color:#3e5069; }
.form-control,.form-select { min-height:43px; border:1px solid #dbe4f0; border-radius:9px; font-size:13px; }
.auth-hint { margin-top:23px; font-size:11px; color:#6f849e; }.auth-hint a { color:#275fc6; }
.validation-summary { color:#be3c4a; font-size:12px; }.flash { margin-bottom:20px; background:#e8f4ef; color:#176e4a; padding:12px 16px; border-radius:9px; }
.narrow { max-width:640px; }.warning-pill,.ok-pill { padding:6px 9px; border-radius:30px; font-size:11px; }
.warning-pill { background:#fff1de; color:#a56b1c; }.ok-pill { background:#e9f7ee; color:#308357; }
@media(max-width:1100px) { .stats-grid { grid-template-columns:repeat(2,1fr); } .panel-grid { grid-template-columns:1fr; } }
@media(max-width:750px) { .sidebar { width:60px; min-width:60px; padding:13px 4px; }.brand { margin:10px 3px 25px; }.brand span:last-child,.side-nav span { display:none; }.side-nav a { justify-content:center; padding:12px 0; }.side-bottom { display:none; }.page-content { padding:22px 15px; }.topbar { padding:0 16px; }.topbar-title { display:none; }.profile { margin-left:auto; gap:8px; }.page-heading { flex-wrap:wrap; }.stats-grid { grid-template-columns:1fr 1fr; }.form-grid { grid-template-columns:1fr; }.form-grid .full { grid-column:span 1; } }
'@

$writes = @(
    @{ Path = 'TradingManagementSystem.Web/Models/AccountModels.cs'; Data = $file0 },
    @{ Path = 'TradingManagementSystem.Web/Controllers/SetupController.cs'; Data = $file1 },
    @{ Path = 'TradingManagementSystem.Web/Controllers/AccountController.cs'; Data = $file2 },
    @{ Path = 'TradingManagementSystem.Web/Controllers/EmployeesController.cs'; Data = $file3 },
    @{ Path = 'TradingManagementSystem.Web/Controllers/CatalogController.cs'; Data = $file4 },
    @{ Path = 'TradingManagementSystem.Web/Controllers/HomeController.cs'; Data = $file5 },
    @{ Path = 'TradingManagementSystem.Web/Program.cs'; Data = $file6 },
    @{ Path = 'TradingManagementSystem.Web/Views/Shared/_Layout.cshtml'; Data = $file7 },
    @{ Path = 'TradingManagementSystem.Web/Views/Home/Index.cshtml'; Data = $file8 },
    @{ Path = 'TradingManagementSystem.Web/Views/Account/Login.cshtml'; Data = $file9 },
    @{ Path = 'TradingManagementSystem.Web/Views/Account/AccessDenied.cshtml'; Data = $file10 },
    @{ Path = 'TradingManagementSystem.Web/Views/Account/ChangePassword.cshtml'; Data = $file11 },
    @{ Path = 'TradingManagementSystem.Web/Views/Setup/Index.cshtml'; Data = $file12 },
    @{ Path = 'TradingManagementSystem.Web/Views/Employees/Index.cshtml'; Data = $file13 },
    @{ Path = 'TradingManagementSystem.Web/Views/Employees/Create.cshtml'; Data = $file14 },
    @{ Path = 'TradingManagementSystem.Web/Views/Catalog/Products.cshtml'; Data = $file15 },
    @{ Path = 'TradingManagementSystem.Web/Views/Catalog/Suppliers.cshtml'; Data = $file16 },
    @{ Path = 'TradingManagementSystem.Web/Views/Catalog/TradingPoints.cshtml'; Data = $file17 },
    @{ Path = 'TradingManagementSystem.Web/Views/Catalog/Purchases.cshtml'; Data = $file18 },
    @{ Path = 'TradingManagementSystem.Web/Views/Catalog/Receipts.cshtml'; Data = $file19 },
    @{ Path = 'TradingManagementSystem.Web/Views/Catalog/Sales.cshtml'; Data = $file20 },
    @{ Path = 'TradingManagementSystem.Web/Views/Catalog/Inventory.cshtml'; Data = $file21 },
    @{ Path = 'TradingManagementSystem.Web/wwwroot/css/tms.css'; Data = $file22 }
)

$replaceSet = @(
    'TradingManagementSystem.Web/Program.cs',
    'TradingManagementSystem.Web/Controllers/HomeController.cs',
    'TradingManagementSystem.Web/Views/Home/Index.cshtml',
    'TradingManagementSystem.Web/Views/Shared/_Layout.cshtml'
)
$roleRules = @{
    'ProductsApiController.cs' = @{ 'HttpPost' = 'Director,SupplyManager'; 'HttpPut' = 'Director,SupplyManager'; 'HttpDelete' = 'Director,SupplyManager' }
    'SuppliersApiController.cs' = @{ 'HttpGet' = 'Director,SupplyManager,TradingPointManager'; 'HttpPost' = 'Director,SupplyManager'; 'HttpPut' = 'Director,SupplyManager'; 'HttpDelete' = 'Director,SupplyManager' }
    'TradingPointsApiController.cs' = @{ 'HttpPost' = 'Director'; 'HttpPut' = 'Director'; 'HttpDelete' = 'Director' }
    'PurchasesApiController.cs' = @{ 'HttpGet' = 'Director,SupplyManager'; 'HttpPost' = 'Director,SupplyManager' }
    'ReceiptsApiController.cs' = @{ 'HttpGet' = 'Director,SupplyManager'; 'HttpPost' = 'Director,SupplyManager' }
    'SalesApiController.cs' = @{ 'HttpGet' = 'Director,TradingPointManager,Seller'; 'HttpPost' = 'Director,TradingPointManager,Seller' }
}
$apiOriginals = @{}
foreach ($entry in $roleRules.GetEnumerator()) {
    $source = Join-Path $api $entry.Key
    $text = [System.IO.File]::ReadAllText($source)
    foreach ($rule in $entry.Value.GetEnumerator()) {
        $pattern = '\[' + $rule.Key + '(?:\([^\]]*\))?\]'
        if (-not [regex]::IsMatch($text,$pattern)) { throw "Cannot find [$($rule.Key)] in $($entry.Key). Nothing changed." }
    }
    $apiOriginals[$entry.Key] = $text
}
$saleSource = $apiOriginals['SalesApiController.cs']
if (-not $saleSource.Contains('SellerId = request.SellerId.Trim()')) { throw 'Unexpected SalesApiController version; need to review seller authorization before installation.' }
if (-not $saleSource.Contains('string.IsNullOrWhiteSpace(request.SellerId) ||')) { throw 'Unexpected SalesApiController validation; no changes made.' }
foreach ($entry in $writes) {
    $path = Join-Path $root $entry.Path
    if ((Test-Path $path) -and ($replaceSet -notcontains $entry.Path)) { throw "File already exists, refusing overwrite: $($entry.Path)" }
}
# Back up every existing file touched before changing source code.
$backup = Join-Path $env:TEMP ('TMS-Auth-UI-backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force -Path $backup | Out-Null
$toBackup = @($replaceSet) + @($roleRules.Keys | ForEach-Object { 'TradingManagementSystem.Web/Controllers/Api/' + $_ })
foreach ($relative in $toBackup) {
    $original = Join-Path $root $relative
    $copyTo = Join-Path $backup $relative
    New-Item -ItemType Directory -Path (Split-Path $copyTo -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $original -Destination $copyTo -Force
}
try {
    foreach ($entry in $writes) {
        $path = Join-Path $root $entry.Path
        New-Item -ItemType Directory -Path (Split-Path $path -Parent) -Force | Out-Null
        Set-Content -LiteralPath $path -Value $entry.Data -Encoding UTF8
    }
    foreach ($entry in $roleRules.GetEnumerator()) {
        $text = $apiOriginals[$entry.Key]
        foreach ($rule in $entry.Value.GetEnumerator()) {
            $pattern = '\[' + $rule.Key + '(?:\([^\]]*\))?\]'
            $auth = '[Microsoft.AspNetCore.Authorization.Authorize(Roles = "' + $rule.Value + '")]' + "`r`n    "
            $text = [regex]::Replace($text, $pattern, [System.Text.RegularExpressions.MatchEvaluator]{ param($m) $auth + $m.Value }, [System.Text.RegularExpressions.RegexOptions]::None)
        }
        if ($entry.Key -eq 'SalesApiController.cs') {
            $text = $text.Replace('var sales = await _context.Sales.Find(_ => true).ToListAsync();', 'var salesFilter = User.IsInRole("Seller") ? Builders<Sale>.Filter.Eq(s => s.SellerId, User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value) : Builders<Sale>.Filter.Empty; var sales = await _context.Sales.Find(salesFilter).ToListAsync();')
            $text = $text.Replace('return sale == null ? NotFound() : Ok(sale);', 'return sale == null ? NotFound() : (User.IsInRole("Seller") && sale.SellerId != User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ? Forbid() : Ok(sale));')
            $text = $text.Replace('string.IsNullOrWhiteSpace(request.SellerId) ||', '')
            $text = $text.Replace('SellerId = request.SellerId.Trim()', 'SellerId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty')
        }
        Set-Content -LiteralPath (Join-Path $api $entry.Key) -Value $text -Encoding UTF8
    }
}
catch {
    Write-Warning "Install encountered an error. Restoring original files from $backup"
    foreach ($relative in $toBackup) {
        Copy-Item -LiteralPath (Join-Path $backup $relative) -Destination (Join-Path $root $relative) -Force
    }
    foreach ($entry in $writes) { if ($replaceSet -notcontains $entry.Path) { Remove-Item -LiteralPath (Join-Path $root $entry.Path) -ErrorAction SilentlyContinue } }
    throw
}
Write-Host ''
Write-Host 'Installed: authentication, 4 roles, dashboard, catalog pages, employee management and UI.' -ForegroundColor Green
Write-Host 'Original files backed up to:' $backup
Write-Host 'NEXT: Ctrl+Shift+B in Visual Studio. Then run HTTPS and open https://localhost:7291/Setup' -ForegroundColor Cyan
Write-Host 'No default password is created. Choose your own password when setting up the Director account.'
