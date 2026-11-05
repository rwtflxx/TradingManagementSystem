using TradingManagementSystem.Application.Services;
using TradingManagementSystem.Domain.Entities;
using TradingManagementSystem.Domain.Enums;
using Xunit;

namespace TradingManagementSystem.Tests;

public class InventoryServiceTests
{
    [Fact]
    public void Service_IncreasesDecreasesAndChecksStock()
    {
        var service = new InventoryService();
        var item = new InventoryItem();
        service.IncreaseStock(item, 10);
        Assert.True(service.HasEnoughStock(item, 8));
        Assert.False(service.HasEnoughStock(item, 11));
        service.DecreaseStock(item, 4);
        Assert.Equal(6, item.Quantity);
    }

    [Fact]
    public void Service_PropagatesInvalidQuantityError()
    {
        var service = new InventoryService();
        var item = new InventoryItem();
        Assert.Throws<ArgumentException>(() => service.IncreaseStock(item, 0));
        Assert.Throws<ArgumentException>(() => service.DecreaseStock(item, 0));
    }
}

public class ReceiptServiceTests
{
    private static (Purchase Purchase, Receipt Receipt, List<InventoryItem> Stock) Prepare(int quantity = 10)
    {
        var purchase = new Purchase { Id = "p", TradingPointId = "tp", SupplierId = "supplier" };
        purchase.AddItem(new PurchaseItem { ProductId = "A", Quantity = quantity, PurchasePrice = 5 });
        var receipt = new Receipt { PurchaseId = purchase.Id };
        var stock = new List<InventoryItem> { new() { ProductId = "A", TradingPointId = "tp" } };
        return (purchase, receipt, stock);
    }

    [Fact]
    public void RegisterReceipt_IncreasesStockAndUpdatesStatus()
    {
        var (purchase, receipt, stock) = Prepare();
        new ReceiptService(new InventoryService()).RegisterReceipt(receipt, purchase, stock);
        Assert.Equal(10, stock[0].Quantity);
        Assert.True(receipt.IsRegistered);
        Assert.Equal(PurchaseStatus.Received, purchase.Status);
    }

    [Fact]
    public void RegisterReceipt_RejectsAlreadyRegisteredReceipt()
    {
        var (purchase, receipt, stock) = Prepare();
        var service = new ReceiptService(new InventoryService());
        service.RegisterReceipt(receipt, purchase, stock);
        Assert.Throws<InvalidOperationException>(() => service.RegisterReceipt(receipt, purchase, stock));
        Assert.Equal(10, stock[0].Quantity);
    }

    [Fact]
    public void RegisterReceipt_RejectsInvalidPurchaseItemQuantity()
    {
        var (purchase, receipt, stock) = Prepare();
        purchase.Items.Clear();
        purchase.Items.Add(new PurchaseItem { ProductId = "A", Quantity = 0 });
        Assert.Throws<InvalidOperationException>(() => new ReceiptService(new InventoryService()).RegisterReceipt(receipt, purchase, stock));
        Assert.False(receipt.IsRegistered);
        Assert.Equal(0, stock[0].Quantity);
    }

    [Fact]
    public void RegisterReceipt_RejectsMissingInventoryItem()
    {
        var (purchase, receipt, _) = Prepare();
        Assert.Throws<InvalidOperationException>(() => new ReceiptService(new InventoryService()).RegisterReceipt(receipt, purchase, new List<InventoryItem>()));
        Assert.False(receipt.IsRegistered);
    }

    [Fact]
    public void RegisterReceipt_RejectsStockFromDifferentTradingPoint()
    {
        var (purchase, receipt, stock) = Prepare();
        stock[0].TradingPointId = "other";
        Assert.Throws<InvalidOperationException>(() => new ReceiptService(new InventoryService()).RegisterReceipt(receipt, purchase, stock));
    }

    [Fact]
    public void RegisterReceipt_ValidatesAllItemsBeforeChangingStock()
    {
        var (purchase, receipt, stock) = Prepare();
        purchase.AddItem(new PurchaseItem { ProductId = "B", Quantity = 1, PurchasePrice = 1 });
        Assert.Throws<InvalidOperationException>(() => new ReceiptService(new InventoryService()).RegisterReceipt(receipt, purchase, stock));
        Assert.Equal(0, stock[0].Quantity);
        Assert.False(receipt.IsRegistered);
    }

    [Fact]
    public void RegisterReceipt_HandlesMultipleProducts()
    {
        var (purchase, receipt, stock) = Prepare();
        purchase.AddItem(new PurchaseItem { ProductId = "B", Quantity = 4, PurchasePrice = 2 });
        stock.Add(new InventoryItem { ProductId = "B", TradingPointId = "tp" });
        new ReceiptService(new InventoryService()).RegisterReceipt(receipt, purchase, stock);
        Assert.Equal(10, stock[0].Quantity);
        Assert.Equal(4, stock[1].Quantity);
    }
}

public class SaleServiceTests
{
    private static (Sale Sale, List<InventoryItem> Stock) Prepare(int stockQuantity = 10, int saleQuantity = 3)
    {
        var sale = new Sale { Id = "s", TradingPointId = "tp", SellerId = "seller" };
        sale.AddItem(new SaleItem { ProductId = "A", Quantity = saleQuantity, UnitPrice = 50 });
        var item = new InventoryItem { ProductId = "A", TradingPointId = "tp" };
        item.Increase(stockQuantity);
        return (sale, new List<InventoryItem> { item });
    }

    [Fact]
    public void CompleteSale_DecreasesStockAndMarksSaleCompleted()
    {
        var (sale, stock) = Prepare();
        new SaleService(new InventoryService()).CompleteSale(sale, stock);
        Assert.Equal(7, stock[0].Quantity);
        Assert.True(sale.IsCompleted);
        Assert.Equal(150m, sale.TotalAmount);
    }

    [Fact]
    public void CompleteSale_RejectsAlreadyCompletedSale()
    {
        var (sale, stock) = Prepare();
        var service = new SaleService(new InventoryService());
        service.CompleteSale(sale, stock);
        Assert.Throws<InvalidOperationException>(() => service.CompleteSale(sale, stock));
        Assert.Equal(7, stock[0].Quantity);
    }

    [Fact]
    public void CompleteSale_RejectsInsufficientStock()
    {
        var (sale, stock) = Prepare(2, 3);
        Assert.Throws<InvalidOperationException>(() => new SaleService(new InventoryService()).CompleteSale(sale, stock));
        Assert.Equal(2, stock[0].Quantity);
        Assert.False(sale.IsCompleted);
    }

    [Fact]
    public void CompleteSale_RejectsMissingInventory()
    {
        var (sale, _) = Prepare();
        Assert.Throws<InvalidOperationException>(() => new SaleService(new InventoryService()).CompleteSale(sale, new List<InventoryItem>()));
        Assert.False(sale.IsCompleted);
    }

    [Fact]
    public void CompleteSale_RejectsDifferentTradingPoint()
    {
        var (sale, stock) = Prepare();
        stock[0].TradingPointId = "other";
        Assert.Throws<InvalidOperationException>(() => new SaleService(new InventoryService()).CompleteSale(sale, stock));
        Assert.Equal(10, stock[0].Quantity);
    }

    [Fact]
    public void CompleteSale_ValidatesAllProductsBeforeChangingStock()
    {
        var (sale, stock) = Prepare();
        sale.AddItem(new SaleItem { ProductId = "B", Quantity = 1, UnitPrice = 2 });
        Assert.Throws<InvalidOperationException>(() => new SaleService(new InventoryService()).CompleteSale(sale, stock));
        Assert.Equal(10, stock[0].Quantity);
    }

    [Fact]
    public void CompleteSale_HandlesMultipleProducts()
    {
        var (sale, stock) = Prepare();
        sale.AddItem(new SaleItem { ProductId = "B", Quantity = 2, UnitPrice = 5 });
        var second = new InventoryItem { ProductId = "B", TradingPointId = "tp" };
        second.Increase(4);
        stock.Add(second);
        new SaleService(new InventoryService()).CompleteSale(sale, stock);
        Assert.Equal(7, stock[0].Quantity);
        Assert.Equal(2, stock[1].Quantity);
    }

    [Fact]
    public void CompleteSale_RejectsDuplicateLinesThatTogetherExceedStockWithoutPartialWrite()
    {
        var (sale, stock) = Prepare(5, 3);
        sale.AddItem(new SaleItem { ProductId = "A", Quantity = 3, UnitPrice = 50 });
        Assert.Throws<InvalidOperationException>(() => new SaleService(new InventoryService()).CompleteSale(sale, stock));
        Assert.Equal(5, stock[0].Quantity);
        Assert.False(sale.IsCompleted);
    }

    [Fact]
    public void CompleteSale_AcceptsDuplicateLinesWhenCombinedStockIsEnough()
    {
        var (sale, stock) = Prepare(10, 3);
        sale.AddItem(new SaleItem { ProductId = "A", Quantity = 3, UnitPrice = 50 });
        new SaleService(new InventoryService()).CompleteSale(sale, stock);
        Assert.Equal(4, stock[0].Quantity);
    }

    [Fact]
    public void CompleteSale_RejectsNonPositiveQuantityFromDirectListMutation()
    {
        var (sale, stock) = Prepare();
        sale.Items[0].Quantity = 0;
        Assert.Throws<InvalidOperationException>(() => new SaleService(new InventoryService()).CompleteSale(sale, stock));
        Assert.Equal(10, stock[0].Quantity);
    }

    [Fact]
    public void CompleteSale_RejectsEmptySaleWithoutChangingStock()
    {
        var (sale, stock) = Prepare();
        sale.Items.Clear();
        Assert.Throws<InvalidOperationException>(() => new SaleService(new InventoryService()).CompleteSale(sale, stock));
        Assert.Equal(10, stock[0].Quantity);
    }
}
