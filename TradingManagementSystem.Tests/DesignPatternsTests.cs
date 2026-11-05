using TradingManagementSystem.Application.Observers;
using TradingManagementSystem.Application.Services;
using TradingManagementSystem.Application.Strategies;
using TradingManagementSystem.Domain.Entities;
using Xunit;

namespace TradingManagementSystem.Tests;

public sealed class DesignPatternsTests
{
    [Theory]
    [InlineData(5, 0, 10)]
    [InlineData(5, 3, 47)]
    [InlineData(-5, 0, 0)]
    public void StandardStrategy_CalculatesStockTarget(int minimum, int dailySales, int expected)
    {
        Assert.Equal(expected, new StandardTargetStockStrategy().CalculateTarget(minimum, dailySales));
    }

    [Fact]
    public void StandardStrategy_RejectsInvalidPlanningHorizon()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StandardTargetStockStrategy(0));
    }

    [Fact]
    public void SeasonalStrategy_ChangesForecast()
    {
        Assert.Equal(89, new SeasonalTargetStockStrategy(2m).CalculateTarget(5, 3));
    }

    [Fact]
    public void SeasonalStrategy_RejectsInvalidMultiplier()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SeasonalTargetStockStrategy(0m));
    }

    [Fact]
    public void SmartInventory_RejectsNullStrategy()
    {
        Assert.Throws<ArgumentNullException>(() => new SmartInventoryService(null!));
    }

    [Fact]
    public void SmartInventory_UsesInjectedStrategy()
    {
        var stock = new InventoryItem { ProductId = "keyboard", TradingPointId = "store", MinimumQuantity = 5 };
        stock.Increase(3);
        var result = Assert.Single(new SmartInventoryService(new FixedTargetStrategy()).Analyze(
            new[] { stock }, Array.Empty<Sale>(), DateTime.UtcNow));
        Assert.Equal(100, result.SuggestedTargetQuantity);
        Assert.Equal(97, result.SuggestedPurchaseQuantity);
    }

    [Fact]
    public void SmartInventory_DefaultStrategyMaintainsEarlierBehaviour()
    {
        var stock = new InventoryItem { ProductId = "keyboard", TradingPointId = "store", MinimumQuantity = 5 };
        stock.Increase(3);
        var result = Assert.Single(new SmartInventoryService().Analyze(
            new[] { stock }, Array.Empty<Sale>(), DateTime.UtcNow));
        Assert.Equal(10, result.SuggestedTargetQuantity);
        Assert.Equal(7, result.SuggestedPurchaseQuantity);
    }

    [Fact]
    public void Observer_ReceivesSuccessfulIncreaseWithPreviousQuantity()
    {
        var service = new InventoryService();
        var observer = new RecordingObserver();
        using var subscription = service.Subscribe(observer);
        var stock = NewStock();
        service.IncreaseStock(stock, 8);
        var change = Assert.Single(observer.Events);
        Assert.Equal(0, change.OldQuantity);
        Assert.Equal(8, change.NewQuantity);
        Assert.Equal("keyboard", change.ProductId);
        Assert.Equal("store", change.TradingPointId);
    }

    [Fact]
    public void Observer_ReceivesSuccessfulDecrease()
    {
        var service = new InventoryService();
        var observer = new RecordingObserver();
        using var subscription = service.Subscribe(observer);
        var stock = NewStock();
        stock.Increase(10);
        service.DecreaseStock(stock, 5);
        var change = Assert.Single(observer.Events);
        Assert.Equal(10, change.OldQuantity);
        Assert.Equal(5, change.NewQuantity);
    }

    [Fact]
    public void Observer_DoesNotPublishAfterFailedMutation()
    {
        var service = new InventoryService();
        var observer = new RecordingObserver();
        using var subscription = service.Subscribe(observer);
        var stock = NewStock();
        Assert.Throws<ArgumentException>(() => service.IncreaseStock(stock, 0));
        Assert.Throws<InvalidOperationException>(() => service.DecreaseStock(stock, 1));
        Assert.Empty(observer.Events);
    }

    [Fact]
    public void Observer_UnsubscribeStopsNotifications()
    {
        var service = new InventoryService();
        var observer = new RecordingObserver();
        var subscription = service.Subscribe(observer);
        subscription.Dispose();
        subscription.Dispose();
        service.IncreaseStock(NewStock(), 3);
        Assert.Empty(observer.Events);
    }

    [Fact]
    public void Observer_RejectsNullSubscription()
    {
        Assert.Throws<ArgumentNullException>(() => new InventoryService().Subscribe(null!));
    }

    [Fact]
    public void LowStockObserver_EmitsOnlyWhenCrossingThreshold()
    {
        var service = new InventoryService();
        var alert = new LowStockAlertObserver();
        using var subscription = service.Subscribe(alert);
        var stock = NewStock();
        service.IncreaseStock(stock, 10);
        service.DecreaseStock(stock, 5);
        service.DecreaseStock(stock, 1);
        var stored = Assert.Single(alert.Alerts);
        Assert.Equal(5, stored.Quantity);
        Assert.Equal(5, stored.MinimumQuantity);
    }

    [Fact]
    public void LowStockObserver_IgnoresHealthyAndAlreadyCriticalChanges()
    {
        var alert = new LowStockAlertObserver();
        alert.OnStockChanged(new StockChangedEvent("a", "tp", 20, 15, 5));
        alert.OnStockChanged(new StockChangedEvent("a", "tp", 4, 3, 5));
        Assert.Empty(alert.Alerts);
    }

    [Fact]
    public void LowStockObserver_RetainsOnlyMostRecentHundredAlerts()
    {
        var alert = new LowStockAlertObserver();
        for (var i = 0; i < 105; i++)
            alert.OnStockChanged(new StockChangedEvent(i.ToString(), "store", 10, 4, 5));
        Assert.Equal(100, alert.Alerts.Count);
        Assert.Equal("5", alert.Alerts[0].ProductId);
    }

    [Fact]
    public void SaleService_PublishesLowStockChangeToSubscribedObserver()
    {
        var inventoryService = new InventoryService();
        var alert = new LowStockAlertObserver();
        using var subscription = inventoryService.Subscribe(alert);
        var stock = NewStock();
        stock.Increase(8);
        var sale = new Sale { TradingPointId = "store", SellerId = "seller" };
        sale.AddItem(new SaleItem { ProductId = "keyboard", Quantity = 4, UnitPrice = 5m });
        new SaleService(inventoryService).CompleteSale(sale, new List<InventoryItem> { stock });
        Assert.True(sale.IsCompleted);
        Assert.Equal(4, stock.Quantity);
        Assert.Single(alert.Alerts);
    }

    private static InventoryItem NewStock() => new()
    {
        ProductId = "keyboard", TradingPointId = "store", MinimumQuantity = 5
    };

    private sealed class FixedTargetStrategy : ITargetStockStrategy
    {
        public int CalculateTarget(int minimumQuantity, decimal averageDailySales) => 100;
    }

    private sealed class RecordingObserver : IStockObserver
    {
        public List<StockChangedEvent> Events { get; } = new();
        public void OnStockChanged(StockChangedEvent change) => Events.Add(change);
    }
}
