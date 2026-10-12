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

        foreach (var saleItem in sale.Items)
        {
            var inventoryItem = inventoryItems.FirstOrDefault(
                i => i.ProductId == saleItem.ProductId &&
                     i.TradingPointId == sale.TradingPointId);

            if (inventoryItem == null)
            {
                throw new InvalidOperationException(
                    "Inventory item was not found.");
            }

            if (!_inventoryService.HasEnoughStock(
                    inventoryItem,
                    saleItem.Quantity))
            {
                throw new InvalidOperationException(
                    "Not enough products in stock.");
            }
        }

        foreach (var saleItem in sale.Items)
        {
            var inventoryItem = inventoryItems.First(
                i => i.ProductId == saleItem.ProductId &&
                     i.TradingPointId == sale.TradingPointId);

            _inventoryService.DecreaseStock(
                inventoryItem,
                saleItem.Quantity);
        }

        sale.Complete();
    }
}
