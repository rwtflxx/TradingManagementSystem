using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using MongoDB.Driver;
using System.Security.Claims;
using TradingManagementSystem.Application.Services;
using TradingManagementSystem.Application.Strategies;
using TradingManagementSystem.Domain.Repositories;
using TradingManagementSystem.Infrastructure.Repositories;
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
            if (current == null || current.Role.ToString() != ctx.Principal?.FindFirstValue(ClaimTypes.Role) || (current.TradingPointId ?? string.Empty) != (ctx.Principal?.FindFirstValue("sf.point") ?? string.Empty))
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
builder.Services.AddScoped<IProductRepository, MongoProductRepository>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<ReceiptService>();
builder.Services.AddScoped<SaleService>();
builder.Services.AddSingleton<ITargetStockStrategy>(new StandardTargetStockStrategy());
builder.Services.AddScoped<SmartInventoryService>(sp =>
    new SmartInventoryService(sp.GetRequiredService<ITargetStockStrategy>()));

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
