using System;
using System.Collections.Generic;
using System.Text;

namespace TradingManagementSystem.Domain.Entities;

public class SaleItem
{
    public string Id { get; set; } = string.Empty;

    public string ProductId { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal GetSubtotal()
    {
        return Quantity * UnitPrice;
    }
}
