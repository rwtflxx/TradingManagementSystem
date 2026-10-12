using System;
using System.Collections.Generic;
using System.Text;

namespace TradingManagementSystem.Domain.Entities;

public class Product
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Sku { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public decimal SellingPrice { get; set; }

    public string Description { get; set; } = string.Empty;

    public void UpdateData(
        string name,
        string category,
        decimal sellingPrice,
        string description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Product name cannot be empty.");
        }

        if (sellingPrice < 0)
        {
            throw new ArgumentException(
                "Selling price cannot be negative.");
        }

        Name = name;
        Category = category;
        SellingPrice = sellingPrice;
        Description = description;
    }
}