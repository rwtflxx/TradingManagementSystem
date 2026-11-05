using System;
using System.Collections.Generic;
using System.Text;

using TradingManagementSystem.Domain.Entities;
using TradingManagementSystem.Application.Observers;

namespace TradingManagementSystem.Application.Services;

public class InventoryService
{
    private readonly object _observerGate = new();
    private readonly List<IStockObserver> _observers = new();

    /// <summary>
    /// Observer subscription for in-memory stock mutations. This is NOT a MongoDB commit event.
    /// The production Web app does not automatically subscribe before persistence succeeds.
    /// </summary>
    public IDisposable Subscribe(IStockObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        lock (_observerGate) _observers.Add(observer);
        return new Subscription(() =>
        {
            lock (_observerGate) _observers.Remove(observer);
        });
    }

    private void Notify(InventoryItem item, int oldQuantity)
    {
        IStockObserver[] listeners;
        lock (_observerGate) listeners = _observers.ToArray();
        if (listeners.Length == 0) return;
        var change = new StockChangedEvent(
            item.ProductId, item.TradingPointId, oldQuantity,
            item.Quantity, item.MinimumQuantity);
        foreach (var observer in listeners) observer.OnStockChanged(change);
    }

    private sealed class Subscription : IDisposable
    {
        private Action? _unsubscribe;
        public Subscription(Action unsubscribe) => _unsubscribe = unsubscribe;
        public void Dispose() => System.Threading.Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
    }

    public void IncreaseStock(
        InventoryItem inventoryItem,
        int quantity)
    {
        var previous = inventoryItem.Quantity;
        inventoryItem.Increase(quantity);
        Notify(inventoryItem, previous);
    }

    public void DecreaseStock(
        InventoryItem inventoryItem,
        int quantity)
    {
        var previous = inventoryItem.Quantity;
        inventoryItem.Decrease(quantity);
        Notify(inventoryItem, previous);
    }

    public bool HasEnoughStock(
        InventoryItem inventoryItem,
        int quantity)
    {
        return inventoryItem.HasEnough(quantity);
    }
}
