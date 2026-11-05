using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TradingManagementSystem.Domain.Entities;
using TradingManagementSystem.Domain.Enums;
using TradingManagementSystem.Infrastructure.Data;

namespace TradingManagementSystem.Web.Controllers;

public class CatalogController : Controller
{
    private readonly MongoDbContext _db;
    public CatalogController(MongoDbContext db) => _db = db;

    public async Task<IActionResult> Products()
    {
        var actor = await CurrentEmployeeAsync();
        if (actor == null) return Unauthorized();
        if (actor.Role == EmployeeRole.Seller)
        {
            if (string.IsNullOrWhiteSpace(actor.TradingPointId)) return Forbid();
            var stock = await _db.InventoryItems.Find(i => i.TradingPointId == actor.TradingPointId && i.Quantity > 0).ToListAsync();
            var ids = stock.Select(i => i.ProductId).Distinct().ToList();
            return View(await _db.Products.Find(Builders<Product>.Filter.In(p => p.Id, ids)).ToListAsync());
        }
        return View(await _db.Products.Find(_ => true).ToListAsync());
    }

    [Authorize(Roles = "SupplyManager")]
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

    [Authorize(Roles = "Director,SupplyManager")]
    public async Task<IActionResult> Suppliers() =>
        View(await _db.Suppliers.Find(_ => true).ToListAsync());

    [Authorize(Roles = "SupplyManager")]
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

    public async Task<IActionResult> TradingPoints()
    {
        var employee = await CurrentEmployeeAsync();
        if (employee == null) return Unauthorized();
        var filter = employee.Role == EmployeeRole.Seller || employee.Role == EmployeeRole.TradingPointManager
            ? Builders<TradingPoint>.Filter.Eq(p => p.Id, employee.TradingPointId)
            : Builders<TradingPoint>.Filter.Empty;
        return View(await _db.TradingPoints.Find(filter).ToListAsync());
    }

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

    [Authorize(Roles = "Director,SupplyManager,TradingPointManager")]
    public async Task<IActionResult> Purchases()
    {
        var actor = await CurrentEmployeeAsync();
        if (actor == null) return Unauthorized();
        var filter = actor.Role == EmployeeRole.TradingPointManager
            ? Builders<Purchase>.Filter.Eq(p => p.TradingPointId, actor.TradingPointId)
            : Builders<Purchase>.Filter.Empty;
        return View(await _db.Purchases.Find(filter).ToListAsync());
    }

    [Authorize(Roles = "Director,SupplyManager")]
    public async Task<IActionResult> Receipts() =>
        View(await _db.Receipts.Find(_ => true).ToListAsync());

    [Authorize(Roles = "Director,TradingPointManager,Seller")]
    public async Task<IActionResult> Sales()
    {
        var sellerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var employee = await CurrentEmployeeAsync();
        if (employee == null) return Unauthorized();
        var filter = User.IsInRole("Seller")
            ? Builders<Sale>.Filter.Eq(x => x.SellerId, sellerId)
            : User.IsInRole("TradingPointManager")
                ? Builders<Sale>.Filter.Eq(x => x.TradingPointId, employee.TradingPointId)
                : Builders<Sale>.Filter.Empty;
        return View(await _db.Sales.Find(filter).ToListAsync());
    }

    public async Task<IActionResult> Inventory()
    {
        var employee = await CurrentEmployeeAsync();
        if (employee == null) return Unauthorized();
        var filter = employee.Role == EmployeeRole.Seller || employee.Role == EmployeeRole.TradingPointManager
            ? Builders<InventoryItem>.Filter.Eq(i => i.TradingPointId, employee.TradingPointId)
            : Builders<InventoryItem>.Filter.Empty;
        return View(await _db.InventoryItems.Find(filter).ToListAsync());
    }

    private async Task<Employee?> CurrentEmployeeAsync()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(id)) return null;
        return await _db.Employees.Find(e => e.Id == id && e.IsActive).FirstOrDefaultAsync();
    }
}
