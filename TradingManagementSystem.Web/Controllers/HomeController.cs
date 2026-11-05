using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TradingManagementSystem.Domain.Enums;
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
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var actor = await _db.Employees.Find(e => e.Id == userId && e.IsActive).FirstOrDefaultAsync();
        if (actor == null) return Unauthorized();
        var localRole = actor.Role == EmployeeRole.Seller || actor.Role == EmployeeRole.TradingPointManager;
        var stockFilter = localRole
            ? Builders<InventoryItem>.Filter.Eq(i => i.TradingPointId, actor.TradingPointId)
            : Builders<InventoryItem>.Filter.Empty;
        var inventory = await _db.InventoryItems.Find(stockFilter).ToListAsync();
        var salesFilter = actor.Role switch
        {
            EmployeeRole.Director => Builders<Sale>.Filter.Empty,
            EmployeeRole.TradingPointManager => Builders<Sale>.Filter.Eq(s => s.TradingPointId, actor.TradingPointId),
            EmployeeRole.Seller => Builders<Sale>.Filter.Eq(s => s.SellerId, actor.Id),
            _ => Builders<Sale>.Filter.Eq(s => s.Id, "__none__")
        };
        var pointFilter = localRole
            ? Builders<TradingPoint>.Filter.Eq(p => p.Id, actor.TradingPointId)
            : Builders<TradingPoint>.Filter.Empty;
        var productsCount = actor.Role == EmployeeRole.Seller
            ? (long)inventory.Where(i => i.Quantity > 0).Select(i => i.ProductId).Distinct().Count()
            : await _db.Products.CountDocumentsAsync(Builders<Product>.Filter.Empty);
        var purchaseFilter = actor.Role == EmployeeRole.TradingPointManager
            ? Builders<Purchase>.Filter.Eq(p => p.TradingPointId, actor.TradingPointId)
            : Builders<Purchase>.Filter.Empty;
        var model = new DashboardStats
        {
            Role = actor.Role.ToString(),
            Products = productsCount,
            Suppliers = actor.Role == EmployeeRole.Director || actor.Role == EmployeeRole.SupplyManager
                ? await _db.Suppliers.CountDocumentsAsync(Builders<Supplier>.Filter.Empty) : 0,
            TradingPoints = await _db.TradingPoints.CountDocumentsAsync(pointFilter),
            Purchases = actor.Role != EmployeeRole.Seller
                ? await _db.Purchases.CountDocumentsAsync(purchaseFilter) : 0,
            Sales = await _db.Sales.CountDocumentsAsync(salesFilter),
            Employees = actor.Role == EmployeeRole.Director
                ? await _db.Employees.CountDocumentsAsync(Builders<Employee>.Filter.Empty)
                : actor.Role == EmployeeRole.TradingPointManager
                    ? await _db.Employees.CountDocumentsAsync(e => e.Role == EmployeeRole.Seller && e.TradingPointId == actor.TradingPointId)
                    : 0,
            LowStock = inventory.Count(i => i.Quantity <= i.MinimumQuantity)
        };
        return View(model);
    }

    public IActionResult Privacy() => RedirectToAction(nameof(Index));
    public IActionResult Error() => View(new TradingManagementSystem.Web.Models.ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
}

public class DashboardStats
{
    public string Role { get; set; } = string.Empty;
    public long Employees { get; set; }
    public long Products { get; set; }
    public long Suppliers { get; set; }
    public long TradingPoints { get; set; }
    public long Purchases { get; set; }
    public long Sales { get; set; }
    public int LowStock { get; set; }
}
