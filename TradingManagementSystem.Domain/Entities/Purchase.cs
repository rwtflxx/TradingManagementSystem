using System;
using System.Collections.Generic;
using System.Text;

using TradingManagementSystem.Domain.Enums;

namespace TradingManagementSystem.Domain.Entities;

public class Purchase
{
    public string Id { get; set; } = string.Empty;

    public string SupplierId { get; set; } = string.Empty;

    public string TradingPointId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PurchaseStatus Status { get; set; } = PurchaseStatus.Created;

    public List<PurchaseItem> Items { get; set; } = new();

    public decimal TotalAmount => CalculateTotal();

    public void AddItem(PurchaseItem item)
    {
        if (item.Quantity <= 0)
        {
            throw new ArgumentException(
                "Quantity must be greater than zero.");
        }

        if (item.PurchasePrice < 0)
        {
            throw new ArgumentException(
                "Purchase price cannot be negative.");
        }

        Items.Add(item);
    }

    public decimal CalculateTotal()
    {
        return Items.Sum(item => item.GetSubtotal());
    }

    public void MarkAsOrdered()
    {
        Status = PurchaseStatus.Ordered;
    }

    public void MarkAsReceived()
    {
        Status = PurchaseStatus.Received;
    }

    public void Cancel()
    {
        Status = PurchaseStatus.Cancelled;
    }
}
