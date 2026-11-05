using TradingManagementSystem.Application.Services;
using TradingManagementSystem.Domain.Entities;
using Xunit;

namespace TradingManagementSystem.Tests;

public class SmartInventoryServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);

    private static InventoryItem Stock(string product, string point, int quantity, int min = 5)
    {
        var result = new InventoryItem
        {
            ProductId = product,
            TradingPointId = point,
            MinimumQuantity = min
        };
        if (quantity > 0)
            result.Increase(quantity);
        return result;
    }

    private static Sale SaleAt(string product, string point, int quantity, DateTime date, bool completed = true)
    {
        var sale = new Sale { TradingPointId = point, SellerId = "employee", CreatedAt = date };
        sale.AddItem(new SaleItem { ProductId = product, Quantity = quantity, UnitPrice = 10m });
        if (completed)
            sale.Complete();
        return sale;
    }

    private static InventoryRecommendation GetOne(IEnumerable<InventoryItem> stock, IEnumerable<Sale>? sales = null) =>
        Assert.Single(new SmartInventoryService().Analyze(stock, sales ?? Array.Empty<Sale>(), Now));

    [Fact]
    public void Analyze_RejectsNullStockAndSales()
    {
        var service = new SmartInventoryService();
        Assert.Throws<ArgumentNullException>(() => service.Analyze(null!, Array.Empty<Sale>(), Now));
        Assert.Throws<ArgumentNullException>(() => service.Analyze(Array.Empty<InventoryItem>(), null!, Now));
    }

    [Fact]
    public void Analyze_EmptyStock_ReturnsNoRecommendations()
    {
        Assert.Empty(new SmartInventoryService().Analyze(Array.Empty<InventoryItem>(), Array.Empty<Sale>(), Now));
    }

    [Fact]
    public void Analyze_HealthyStock_IsNotAtRisk()
    {
        var r = GetOne(new[] { Stock("keyboard", "store1", 15) });
        Assert.Equal(10, r.SuggestedTargetQuantity);
        Assert.False(r.IsCritical);
        Assert.False(r.IsAtRisk);
        Assert.Equal(0, r.SuggestedPurchaseQuantity);
        Assert.Null(r.SuggestedTransferFromId);
        Assert.Equal(0, r.SuggestedTransferQuantity);
    }

    [Fact]
    public void Analyze_AtMinimum_RecommendsPurchase()
    {
        var r = GetOne(new[] { Stock("keyboard", "store1", 5) });
        Assert.True(r.IsCritical);
        Assert.True(r.IsAtRisk);
        Assert.Equal(5, r.SuggestedPurchaseQuantity);
        Assert.Equal(5, r.MinimumQuantity);
        Assert.Equal(5, r.Quantity);
    }

    [Fact]
    public void Analyze_BelowMinimum_RecommendsBuffer()
    {
        var r = GetOne(new[] { Stock("keyboard", "store1", 2) });
        Assert.Equal(8, r.SuggestedPurchaseQuantity);
        Assert.Equal(10, r.SuggestedTargetQuantity);
    }

    [Fact]
    public void Analyze_RecentDemand_RaisesTargetAndDetectsRiskAboveMinimum()
    {
        var sale = SaleAt("keyboard", "store1", 30, Now.AddDays(-2));
        var r = GetOne(new[] { Stock("keyboard", "store1", 15) }, new[] { sale });
        Assert.Equal(30, r.SoldLast30Days);
        Assert.Equal(1m, r.AverageDailySales);
        Assert.Equal(19, r.SuggestedTargetQuantity);
        Assert.False(r.IsCritical);
        Assert.True(r.IsAtRisk);
        Assert.Equal(4, r.SuggestedPurchaseQuantity);
    }

    [Fact]
    public void Analyze_EnoughForForecast_DoesNotRecommendBuying()
    {
        var sale = SaleAt("keyboard", "store1", 30, Now.AddDays(-2));
        var r = GetOne(new[] { Stock("keyboard", "store1", 25) }, new[] { sale });
        Assert.False(r.IsAtRisk);
        Assert.Equal(0, r.SuggestedPurchaseQuantity);
    }

    [Fact]
    public void Analyze_IgnoresOldFutureAndUnfinishedSales()
    {
        var sales = new[]
        {
            SaleAt("keyboard", "store1", 99, Now.AddDays(-31)),
            SaleAt("keyboard", "store1", 99, Now.AddDays(1)),
            SaleAt("keyboard", "store1", 99, Now.AddDays(-1), completed: false)
        };
        var r = GetOne(new[] { Stock("keyboard", "store1", 15) }, sales);
        Assert.Equal(0, r.SoldLast30Days);
        Assert.Equal(10, r.SuggestedTargetQuantity);
        Assert.False(r.IsAtRisk);
    }

    [Fact]
    public void Analyze_StartOfHistoryWindowIsIncluded()
    {
        var sale = SaleAt("keyboard", "store1", 30, Now.AddDays(-SmartInventoryService.HistoryDays));
        var r = GetOne(new[] { Stock("keyboard", "store1", 15) }, new[] { sale });
        Assert.Equal(30, r.SoldLast30Days);
        Assert.Equal(19, r.SuggestedTargetQuantity);
    }

    [Fact]
    public void Analyze_IgnoresOtherProductsAndStores()
    {
        var sales = new[]
        {
            SaleAt("other", "store1", 90, Now.AddDays(-1)),
            SaleAt("keyboard", "store2", 90, Now.AddDays(-1))
        };
        var r = GetOne(new[] { Stock("keyboard", "store1", 15) }, sales);
        Assert.Equal(0, r.SoldLast30Days);
        Assert.False(r.IsAtRisk);
    }

    [Fact]
    public void Analyze_DoesNotCountNegativeQuantityIfMalformedSaleIsLoaded()
    {
        var sale = SaleAt("keyboard", "store1", 1, Now.AddDays(-1));
        sale.Items[0].Quantity = -50;
        var r = GetOne(new[] { Stock("keyboard", "store1", 15) }, new[] { sale });
        Assert.Equal(0, r.SoldLast30Days);
        Assert.Equal(0m, r.AverageDailySales);
    }

    [Fact]
    public void Analyze_SumsMatchingSalesLines()
    {
        var sale = SaleAt("keyboard", "store1", 5, Now.AddDays(-1));
        sale.AddItem(new SaleItem { ProductId = "keyboard", Quantity = 7, UnitPrice = 10m });
        var r = GetOne(new[] { Stock("keyboard", "store1", 9) }, new[] { sale });
        Assert.Equal(12, r.SoldLast30Days);
        Assert.Equal(0.4m, r.AverageDailySales);
        Assert.Equal(11, r.SuggestedTargetQuantity);
        Assert.Equal(2, r.SuggestedPurchaseQuantity);
    }

    [Fact]
    public void Analyze_TransfersAllNeededUnitsWhenAnotherStoreHasSurplus()
    {
        var results = new SmartInventoryService().Analyze(
            new[] { Stock("keyboard", "destination", 2), Stock("keyboard", "source", 30) },
            Array.Empty<Sale>(), Now);
        var r = Assert.Single(results.Where(x => x.TradingPointId == "destination"));
        Assert.Equal("source", r.SuggestedTransferFromId);
        Assert.Equal(8, r.SuggestedTransferQuantity);
        Assert.Equal(0, r.SuggestedPurchaseQuantity);
    }

    [Fact]
    public void Analyze_PartialTransfer_StillSuggestsBuyingRemainingQuantity()
    {
        var results = new SmartInventoryService().Analyze(
            new[] { Stock("keyboard", "destination", 2), Stock("keyboard", "source", 13) },
            Array.Empty<Sale>(), Now);
        var r = Assert.Single(results.Where(x => x.TradingPointId == "destination"));
        Assert.Equal("source", r.SuggestedTransferFromId);
        Assert.Equal(3, r.SuggestedTransferQuantity);
        Assert.Equal(5, r.SuggestedPurchaseQuantity);
    }

    [Fact]
    public void Analyze_ChoosesSourceWithMostAvailableStock()
    {
        var results = new SmartInventoryService().Analyze(
            new[]
            {
                Stock("keyboard", "destination", 2),
                Stock("keyboard", "sourceA", 12),
                Stock("keyboard", "sourceB", 15)
            }, Array.Empty<Sale>(), Now);
        var r = Assert.Single(results.Where(x => x.TradingPointId == "destination"));
        Assert.Equal("sourceB", r.SuggestedTransferFromId);
        Assert.Equal(5, r.SuggestedTransferQuantity);
        Assert.Equal(3, r.SuggestedPurchaseQuantity);
    }

    [Fact]
    public void Analyze_DifferentProductCannotBeTransferSource()
    {
        var results = new SmartInventoryService().Analyze(
            new[] { Stock("keyboard", "destination", 2), Stock("mouse", "source", 30) },
            Array.Empty<Sale>(), Now);
        var r = Assert.Single(results.Where(x => x.TradingPointId == "destination"));
        Assert.Null(r.SuggestedTransferFromId);
        Assert.Equal(0, r.SuggestedTransferQuantity);
        Assert.Equal(8, r.SuggestedPurchaseQuantity);
    }

    [Fact]
    public void Analyze_SourceWithHighDemandDoesNotOfferItsProtectedStock()
    {
        var sale = SaleAt("keyboard", "source", 30, Now.AddDays(-1));
        var results = new SmartInventoryService().Analyze(
            new[] { Stock("keyboard", "destination", 2), Stock("keyboard", "source", 13) },
            new[] { sale }, Now);
        var r = Assert.Single(results.Where(x => x.TradingPointId == "destination"));
        Assert.Null(r.SuggestedTransferFromId);
        Assert.Equal(8, r.SuggestedPurchaseQuantity);
    }

    [Fact]
    public void Analyze_ZeroMinimumAndZeroStock_NeverRecommendsNegativePurchase()
    {
        var r = GetOne(new[] { Stock("keyboard", "store1", 0, 0) });
        Assert.True(r.IsCritical);
        Assert.Equal(0, r.SuggestedPurchaseQuantity);
        Assert.Equal(0, r.SuggestedTargetQuantity);
    }

    [Fact]
    public void Analyze_NegativeMinimumIsClampedToZero()
    {
        var r = GetOne(new[] { Stock("keyboard", "store1", 2, -9) });
        Assert.Equal(0, r.MinimumQuantity);
        Assert.False(r.IsCritical);
        Assert.False(r.IsAtRisk);
    }

    [Fact]
    public void Analyze_ReturnsCriticalThenAtRiskThenHealthy()
    {
        var sales = new[] { SaleAt("risk", "store1", 30, Now.AddDays(-2)) };
        var results = new SmartInventoryService().Analyze(new[]
        {
            Stock("healthy", "store1", 25),
            Stock("risk", "store1", 15),
            Stock("critical", "store1", 2)
        }, sales, Now);
        Assert.Equal(new[] { "critical", "risk", "healthy" }, results.Select(x => x.ProductId));
    }

    [Fact]
    public void Analyze_DoesNotMutateExistingStock()
    {
        var item = Stock("keyboard", "store1", 5);
        var r = GetOne(new[] { item });
        Assert.Equal(5, item.Quantity);
        Assert.Equal(5, r.Quantity);
    }
}
