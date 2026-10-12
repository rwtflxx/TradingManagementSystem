using System;
using System.Collections.Generic;
using System.Text;

using TradingManagementSystem.Domain.Entities;

namespace TradingManagementSystem.Application.Services;

public class ReceiptService
{
    private readonly InventoryService _inventoryService;

    public ReceiptService(InventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    public void RegisterReceipt(
        Receipt receipt,
        Purchase purchase,
        List<InventoryItem> inventoryItems)
    {
        if (receipt.IsRegistered)
        {
            throw new InvalidOperationException(
                "Receipt has already been registered.");
        }

        
        foreach (var purchaseItem in purchase.Items)
        {
            if (purchaseItem.Quantity <= 0)
            {
                throw new InvalidOperationException(
                    "Purchase item quantity must be greater than zero.");
            }

            var inventoryItem = inventoryItems.FirstOrDefault(
                i => i.ProductId == purchaseItem.ProductId &&
                     i.TradingPointId == purchase.TradingPointId);

            if (inventoryItem == null)
            {
                throw new InvalidOperationException(
                    "Inventory item was not found.");
            }
        }

       
        foreach (var purchaseItem in purchase.Items)
        {
            var inventoryItem = inventoryItems.First(
                i => i.ProductId == purchaseItem.ProductId &&
                     i.TradingPointId == purchase.TradingPointId);

            _inventoryService.IncreaseStock(
                inventoryItem,
                purchaseItem.Quantity);
        }

        receipt.Register();
        purchase.MarkAsReceived();
    }
}
