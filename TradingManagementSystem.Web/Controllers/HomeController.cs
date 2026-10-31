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
