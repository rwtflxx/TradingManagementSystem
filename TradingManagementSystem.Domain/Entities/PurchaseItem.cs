using System;
using System.Collections.Generic;
using System.Text;

namespace TradingManagementSystem.Domain.Entities;

public class PurchaseItem
{
    public string Id { get; set; } = string.Empty;

    public string ProductId { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal PurchasePrice { get; set; }

    public decimal GetSubtotal()
    {
        return Quantity * PurchasePrice;
    }
}
