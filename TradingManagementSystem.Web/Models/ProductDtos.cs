namespace TradingManagementSystem.Web.Models.Api;

public class CreateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal SellingPrice { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class UpdateProductRequest
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal SellingPrice { get; set; }
    public string Description { get; set; } = string.Empty;
}