using System;
using System.Collections.Generic;
using System.Text;

using TradingManagementSystem.Domain.Entities;

namespace TradingManagementSystem.Application.Services;

public class InventoryService
{
    public void IncreaseStock(
        InventoryItem inventoryItem,
        int quantity)
    {
        inventoryItem.Increase(quantity);
    }

    public void DecreaseStock(
        InventoryItem inventoryItem,
        int quantity)
    {
        inventoryItem.Decrease(quantity);
    }

    public bool HasEnoughStock(
        InventoryItem inventoryItem,
        int quantity)
    {
        return inventoryItem.HasEnough(quantity);
    }
}
