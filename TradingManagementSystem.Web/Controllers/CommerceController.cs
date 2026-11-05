using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using TradingManagementSystem.Application.Services;
using TradingManagementSystem.Domain.Entities;
using TradingManagementSystem.Domain.Enums;
using TradingManagementSystem.Infrastructure.Data;
using TradingManagementSystem.Web.Models;

namespace TradingManagementSystem.Web.Controllers;

[Authorize]
public sealed class CommerceController : Controller
{
    private readonly MongoDbContext _db;
    private readonly ReceiptService _receipts;
    private readonly SaleService _sales;

    public CommerceController(MongoDbContext db, ReceiptService receipts, SaleService sales)
    {
        _db = db;
        _receipts = receipts;
        _sales = sales;
    }

    private async Task<Employee?> CurrentAsync()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(id)) return null;
        return await _db.Employees.Find(x => x.Id == id && x.IsActive).FirstOrDefaultAsync();
    }

    private async Task LoadPurchaseChoicesAsync()
    {
        ViewBag.Suppliers = await _db.Suppliers.Find(_ => true).ToListAsync();
        ViewBag.Points = await _db.TradingPoints.Find(_ => true).ToListAsync();
        ViewBag.Products = await _db.Products.Find(_ => true).ToListAsync();
    }

    [Authorize(Roles = "SupplyManager")]
    [HttpGet]
    public async Task<IActionResult> NewPurchase(string? productId, string? tradingPointId, int quantity = 1)
    {
        await LoadPurchaseChoicesAsync();
        return View(new PurchaseFormModel
        {
            TradingPointId = tradingPointId ?? string.Empty,
            Items = new List<PurchaseLineForm> { new()
            {
                ProductId = productId ?? string.Empty,
                Quantity = Math.Clamp(quantity, 1, 100000)
            }}
        });
    }

    [Authorize(Roles = "SupplyManager")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> NewPurchase(PurchaseFormModel form)
    {
        var actor = await CurrentAsync();
        if (actor == null) return Unauthorized();
        await LoadPurchaseChoicesAsync();

        if (string.IsNullOrWhiteSpace(form.SupplierId) || string.IsNullOrWhiteSpace(form.TradingPointId))
            return PurchaseError("Вибери постачальника та торгову точку.", form);

        if (!await _db.Suppliers.Find(s => s.Id == form.SupplierId).AnyAsync() ||
            !await _db.TradingPoints.Find(p => p.Id == form.TradingPointId).AnyAsync())
            return PurchaseError("Обраний постачальник або торгова точка не існує.", form);

        var lines = (form.Items ?? new List<PurchaseLineForm>())
            .Where(i => !string.IsNullOrWhiteSpace(i.ProductId)).ToList();
        if (lines.Count == 0 || lines.Count > 50)
            return PurchaseError("Додай від 1 до 50 позицій закупівлі.", form);
        if (lines.Any(i => i.Quantity <= 0 || i.PurchasePrice < 0) ||
            lines.Select(i => i.ProductId).Distinct(StringComparer.Ordinal).Count() != lines.Count)
            return PurchaseError("Перевір кількість, закупівельну ціну та відсутність повторних товарів.", form);

        var validProductIds = await _db.Products.Find(_ => true).Project(p => p.Id).ToListAsync();
        if (lines.Any(i => !validProductIds.Contains(i.ProductId)))
            return PurchaseError("Один з вибраних товарів не існує.", form);

        var purchase = new Purchase
        {
            Id = Guid.NewGuid().ToString(),
            SupplierId = form.SupplierId,
            TradingPointId = form.TradingPointId
        };
        foreach (var line in lines)
            purchase.AddItem(new PurchaseItem
            {
                Id = Guid.NewGuid().ToString(),
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                PurchasePrice = line.PurchasePrice
            });

        await _db.Purchases.InsertOneAsync(purchase);
        TempData["Flash"] = "Закупівлю створено. Залишки зміняться лише після реєстрації надходження.";
        return RedirectToAction("Purchases", "Catalog");
    }

    private IActionResult PurchaseError(string message, PurchaseFormModel form)
    {
        ViewBag.Error = message;
        return View(form);
    }

    [Authorize(Roles = "SupplyManager")]
    [HttpGet]
    public async Task<IActionResult> RegisterReceipt(string? purchaseId)
    {
        await LoadReceiptChoicesAsync();
        return View(new ReceiptFormModel { PurchaseId = purchaseId ?? string.Empty });
    }

    private async Task LoadReceiptChoicesAsync()
    {
        ViewBag.Purchases = await _db.Purchases
            .Find(p => p.Status != PurchaseStatus.Received && p.Status != PurchaseStatus.Cancelled)
            .ToListAsync();
    }

    [Authorize(Roles = "SupplyManager")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterReceipt(ReceiptFormModel form)
    {
        var actor = await CurrentAsync();
        if (actor == null) return Unauthorized();
        await LoadReceiptChoicesAsync();

        var purchase = await _db.Purchases.Find(p => p.Id == form.PurchaseId).FirstOrDefaultAsync();
        if (purchase == null) return ReceiptError("Закупівлю не знайдено.", form);
        if (purchase.Status == PurchaseStatus.Received || purchase.Status == PurchaseStatus.Cancelled)
            return ReceiptError("Цю закупівлю вже отримано або скасовано.", form);
        if (await _db.Receipts.Find(r => r.PurchaseId == purchase.Id).AnyAsync())
            return ReceiptError("Надходження для цієї закупівлі вже зареєстроване.", form);
        if (purchase.Items.Count == 0 || purchase.Items.Any(i => i.Quantity <= 0))
            return ReceiptError("Закупівля не містить коректних позицій.", form);

        var stock = await _db.InventoryItems.Find(i => i.TradingPointId == purchase.TradingPointId).ToListAsync();
        foreach (var id in purchase.Items.Select(i => i.ProductId).Distinct())
        {
            if (!stock.Any(i => i.ProductId == id))
                stock.Add(new InventoryItem
                {
                    Id = Guid.NewGuid().ToString(),
                    ProductId = id,
                    TradingPointId = purchase.TradingPointId,
                    MinimumQuantity = 5
                });
        }
        var receipt = new Receipt { Id = Guid.NewGuid().ToString(), PurchaseId = purchase.Id };
        try
        {
            _receipts.RegisterReceipt(receipt, purchase, stock);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return ReceiptError(ex.Message, form);
        }

        // Keeps the same persistence flow as the existing Receipts API.
        // For production multi-document atomicity, use a MongoDB replica set transaction.
        foreach (var item in stock.Where(i => purchase.Items.Any(p => p.ProductId == i.ProductId)))
            await _db.InventoryItems.ReplaceOneAsync(x => x.Id == item.Id, item, new ReplaceOptions { IsUpsert = true });
        await _db.Purchases.ReplaceOneAsync(x => x.Id == purchase.Id, purchase);
        await _db.Receipts.InsertOneAsync(receipt);

        TempData["Flash"] = "Надходження зареєстровано. Товарні залишки збільшено.";
        return RedirectToAction("Receipts", "Catalog");
    }

    private IActionResult ReceiptError(string message, ReceiptFormModel model)
    {
        ViewBag.Error = message;
        return View(model);
    }

    [Authorize(Roles = "Seller")]
    [HttpGet]
    public async Task<IActionResult> NewSale()
    {
        var actor = await CurrentAsync();
        if (actor == null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(actor.TradingPointId)) return Forbid();
        await LoadSaleChoicesAsync(actor.TradingPointId);
        return View(new SaleFormModel());
    }

    private async Task LoadSaleChoicesAsync(string pointId)
    {
        var stock = await _db.InventoryItems.Find(i => i.TradingPointId == pointId && i.Quantity > 0).ToListAsync();
        var productIds = stock.Select(i => i.ProductId).Distinct().ToList();
        ViewBag.Products = await _db.Products.Find(Builders<Product>.Filter.In(p => p.Id, productIds)).ToListAsync();
        ViewBag.Stock = stock.GroupBy(x => x.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
    }

    [Authorize(Roles = "Seller")]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> NewSale(SaleFormModel form)
    {
        var actor = await CurrentAsync();
        if (actor == null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(actor.TradingPointId)) return Forbid();
        await LoadSaleChoicesAsync(actor.TradingPointId);

        if (!Guid.TryParse(form.RequestId, out _) || !Enum.IsDefined(form.PaymentMethod))
            return SaleError("Некоректні дані операції або спосіб оплати.", form);
        var lines = (form.Items ?? new List<SaleLineForm>())
            .Where(i => !string.IsNullOrWhiteSpace(i.ProductId)).ToList();
        if (lines.Count == 0 || lines.Count > 50 || lines.Any(i => i.Quantity <= 0) ||
            lines.Select(i => i.ProductId).Distinct(StringComparer.Ordinal).Count() != lines.Count)
            return SaleError("Додай від 1 до 50 товарів із додатною кількістю без повторень.", form);
        if (await _db.Sales.Find(s => s.Id == form.RequestId).AnyAsync())
            return SaleError("Цей продаж уже оформлено — повторювати його не потрібно.", form);

        var availableProducts = (List<Product>)ViewBag.Products;
        var stock = await _db.InventoryItems.Find(i => i.TradingPointId == actor.TradingPointId).ToListAsync();
        var sale = new Sale
        {
            Id = form.RequestId,
            SellerId = actor.Id,
            TradingPointId = actor.TradingPointId,
            PaymentMethod = form.PaymentMethod
        };
        foreach (var line in lines)
        {
            var product = availableProducts.FirstOrDefault(p => p.Id == line.ProductId);
            if (product == null) return SaleError("Товар не знайдено серед доступних у твоєму магазині.", form);
            sale.AddItem(new SaleItem
            {
                Id = Guid.NewGuid().ToString(), ProductId = product.Id,
                Quantity = line.Quantity, UnitPrice = product.SellingPrice
            });
        }
        try
        {
            _sales.CompleteSale(sale, stock);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return SaleError(ex.Message, form);
        }

        var applied = new List<(string Id, int Quantity)>();
        try
        {
            foreach (var line in sale.Items)
            {
                var current = stock.First(x => x.ProductId == line.ProductId);
                var filter = Builders<InventoryItem>.Filter.And(
                    Builders<InventoryItem>.Filter.Eq(x => x.Id, current.Id),
                    Builders<InventoryItem>.Filter.Gte(x => x.Quantity, line.Quantity));
                var updated = await _db.InventoryItems.FindOneAndUpdateAsync(
                    filter, Builders<InventoryItem>.Update.Inc(x => x.Quantity, -line.Quantity));
                if (updated == null)
                    throw new InvalidOperationException("Недостатньо товару на складі або залишок уже змінився.");
                applied.Add((current.Id, line.Quantity));
            }
            await _db.Sales.InsertOneAsync(sale);
        }
        catch (Exception)
        {
            foreach (var entry in applied)
                await _db.InventoryItems.UpdateOneAsync(x => x.Id == entry.Id,
                    Builders<InventoryItem>.Update.Inc(x => x.Quantity, entry.Quantity));
            return SaleError("Не вдалося завершити продаж. Перевір залишок і повтори пізніше.", form);
        }

        TempData["Flash"] = $"Продаж завершено. Сума: {sale.TotalAmount:0.00} грн.";
        return RedirectToAction("Sales", "Catalog");
    }

    private IActionResult SaleError(string message, SaleFormModel model)
    {
        ViewBag.Error = message;
        return View(model);
    }
}
