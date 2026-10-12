using System;
using System.Collections.Generic;
using System.Text;

namespace TradingManagementSystem.Domain.Entities;

public class Receipt
{
    public string Id { get; set; } = string.Empty;

    public string PurchaseId { get; set; } = string.Empty;

    public DateTime ReceivedAt { get; private set; }

    public bool IsRegistered { get; private set; }

    public void Register()
    {
        if (IsRegistered)
        {
            throw new InvalidOperationException(
                "Receipt has already been registered.");
        }

        ReceivedAt = DateTime.UtcNow;
        IsRegistered = true;
    }
}
