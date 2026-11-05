namespace TradingManagementSystem.Application.Observers;

/// <summary>In-memory event after InventoryService successfully changes an InventoryItem.</summary>
public sealed record StockChangedEvent(
    string ProductId, string TradingPointId,
    int OldQuantity, int NewQuantity, int MinimumQuantity);

public interface IStockObserver
{
    void OnStockChanged(StockChangedEvent change);
}

public sealed record StockAlert(
    string ProductId, string TradingPointId,
    int Quantity, int MinimumQuantity, DateTime OccurredAtUtc);

/// <summary>
/// Observer: records a low-stock alert only when quantity crosses down to the minimum.
/// Alerts are in memory; this does not imply a committed MongoDB transaction.
/// </summary>
public sealed class LowStockAlertObserver : IStockObserver
{
    public const int MaxStoredAlerts = 100;
    private readonly object _gate = new();
    private readonly List<StockAlert> _alerts = new();

    public IReadOnlyList<StockAlert> Alerts
    {
        get { lock (_gate) return _alerts.ToArray(); }
    }

    public void OnStockChanged(StockChangedEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var minimum = Math.Max(0, change.MinimumQuantity);
        if (change.OldQuantity <= minimum || change.NewQuantity > minimum)
            return;

        lock (_gate)
        {
            _alerts.Add(new StockAlert(change.ProductId, change.TradingPointId,
                change.NewQuantity, minimum, DateTime.UtcNow));
            if (_alerts.Count > MaxStoredAlerts)
                _alerts.RemoveAt(0);
        }
    }
}
