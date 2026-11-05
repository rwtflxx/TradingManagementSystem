using System.Linq;
using System;
using System.Collections.Generic;
using System.Text;

using TradingManagementSystem.Domain.Entities;

namespace TradingManagementSystem.Application.Services;

public class SaleService
{
    private readonly InventoryService _inventoryService;

    public SaleService(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    public void CompleteSale(
        Sale sale,
        List<InventoryItem> inventoryItems)
    {
        if (sale.IsCompleted)
        {
            throw new InvalidOperationException(
                "Sale has already been completed.");
        }

        // Validate the entire sale before changing any inventory quantities.
        if (sale.Items.Count == 0)
        {
            throw new InvalidOperationException("Sale must contain at least one item.");
        }

        foreach (var saleItem in sale.Items)
        {
            if (saleItem.Quantity <= 0)
            {
                throw new InvalidOperationException("Sale item quantity must be greater than zero.");
            }
        }

        // A product can appear on several lines: check its COMBINED demand.
        var requested = sale.Items
            .GroupBy(item => item.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                Quantity = group.Sum(item => (long)item.Quantity)
            })
            .ToList();

        foreach (var request in requested)
        {
            var inventoryItem = inventoryItems.FirstOrDefault(
                i => i.ProductId == request.ProductId &&
                     i.TradingPointId == sale.TradingPointId);

            if (inventoryItem == null)
            {
                throw new InvalidOperationException("Inventory item was not found.");
            }

            if (request.Quantity > int.MaxValue ||
                !_inventoryService.HasEnoughStock(inventoryItem, (int)request.Quantity))
            {
                throw new InvalidOperationException("Not enough products in stock.");
            }
        }

        // Only after every product has been validated do we change stock.
        foreach (var request in requested)
        {
            var inventoryItem = inventoryItems.First(
                i => i.ProductId == request.ProductId &&
                     i.TradingPointId == sale.TradingPointId);

            _inventoryService.DecreaseStock(inventoryItem, (int)request.Quantity);
        }

        sale.Complete();
    }
}
