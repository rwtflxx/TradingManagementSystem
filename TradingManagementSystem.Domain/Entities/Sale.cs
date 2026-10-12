using System;
using System.Collections.Generic;
using System.Text;

using TradingManagementSystem.Domain.Enums;

namespace TradingManagementSystem.Domain.Entities;

public class Sale
{
    public string Id { get; set; } = string.Empty;

    public string SellerId { get; set; } = string.Empty;

    public string TradingPointId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PaymentMethod PaymentMethod { get; set; }

    public List<SaleItem> Items { get; set; } = new();

    public bool IsCompleted { get; private set; }

    public decimal TotalAmount => CalculateTotal();

    public void AddItem(SaleItem item)
    {
        if (item.Quantity <= 0)
        {
            throw new ArgumentException(
                "Quantity must be greater than zero.");
        }

        if (item.UnitPrice < 0)
        {
            throw new ArgumentException(
                "Unit price cannot be negative.");
        }

        Items.Add(item);
    }

    public decimal CalculateTotal()
    {
        return Items.Sum(item => item.GetSubtotal());
    }

    public void Complete()
    {
        if (Items.Count == 0)
        {
            throw new InvalidOperationException(
                "Sale must contain at least one item.");
        }

        IsCompleted = true;
    }
}