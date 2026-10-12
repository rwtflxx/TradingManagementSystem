using System;
using System.Collections.Generic;
using System.Text;

namespace TradingManagementSystem.Domain.Entities;

public class InventoryItem
{
    public string Id { get; set; } = string.Empty;

    public string ProductId { get; set; } = string.Empty;

    public string TradingPointId { get; set; } = string.Empty;

    public int Quantity { get; private set; }

    public int MinimumQuantity { get; set; }

    public void Increase(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException(
                "Quantity must be greater than zero.");
        }

        Quantity += quantity;
    }

    public void Decrease(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException(
                "Quantity must be greater than zero.");
        }

        if (!HasEnough(quantity))
        {
            throw new InvalidOperationException(
                "Not enough products in stock.");
        }

        Quantity -= quantity;
    }

    public bool HasEnough(int quantity)
    {
        return Quantity >= quantity;
    }
}
