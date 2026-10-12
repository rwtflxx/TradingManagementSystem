using TradingManagementSystem.Application.Services;
using TradingManagementSystem.Domain.Entities;
using TradingManagementSystem.Domain.Enums;

Console.WriteLine("=== Trading Management System ===");
Console.WriteLine();

// Створюємо товар
var product = new Product
{
    Id = "P1",
    Name = "Keyboard",
    Sku = "KB-001",
    Category = "Computer accessories",
    SellingPrice = 50
};

// Створюємо торгову точку
var tradingPoint = new TradingPoint
{
    Id = "TP1",
    Name = "Main Store",
    Address = "Ternopil"
};

// Початковий залишок товару
var inventoryItem = new InventoryItem
{
    Id = "INV1",
    ProductId = product.Id,
    TradingPointId = tradingPoint.Id,
    MinimumQuantity = 2
};

var inventoryItems = new List<InventoryItem>
{
    inventoryItem
};

Console.WriteLine(
    $"Initial stock: {inventoryItem.Quantity}");

// Створюємо закупівлю
var purchase = new Purchase
{
    Id = "PUR1",
    SupplierId = "SUP1",
    TradingPointId = tradingPoint.Id
};

purchase.AddItem(new PurchaseItem
{
    Id = "PI1",
    ProductId = product.Id,
    Quantity = 10,
    PurchasePrice = 30
});

Console.WriteLine(
    $"After purchase creation: {inventoryItem.Quantity}");

// Створюємо сервіси
var inventoryService = new InventoryService();

var receiptService =
    new ReceiptService(inventoryService);

var saleService =
    new SaleService(inventoryService);

// Реєструємо фактичне надходження
var receipt = new Receipt
{
    Id = "REC1",
    PurchaseId = purchase.Id
};

receiptService.RegisterReceipt(
    receipt,
    purchase,
    inventoryItems);

Console.WriteLine(
    $"After receipt: {inventoryItem.Quantity}");

// Створюємо продаж
var sale = new Sale
{
    Id = "SALE1",
    SellerId = "EMP1",
    TradingPointId = tradingPoint.Id,
    PaymentMethod = PaymentMethod.Card
};

sale.AddItem(new SaleItem
{
    Id = "SI1",
    ProductId = product.Id,
    Quantity = 3,
    UnitPrice = product.SellingPrice
});

// Завершуємо продаж
saleService.CompleteSale(
    sale,
    inventoryItems);

Console.WriteLine(
    $"After sale: {inventoryItem.Quantity}");

Console.WriteLine();
Console.WriteLine(
    $"Sale total: {sale.TotalAmount}");