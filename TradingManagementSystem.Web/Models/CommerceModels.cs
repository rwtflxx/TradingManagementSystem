using TradingManagementSystem.Domain.Enums;

namespace TradingManagementSystem.Web.Models;

public class PurchaseFormModel
{
    public string SupplierId { get; set; } = string.Empty;
    public string TradingPointId { get; set; } = string.Empty;
    public List<PurchaseLineForm> Items { get; set; } = new() { new() };
}

public class PurchaseLineForm
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal PurchasePrice { get; set; }
}

public class ReceiptFormModel
{
    public string PurchaseId { get; set; } = string.Empty;
}

public class SaleFormModel
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString();
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public List<SaleLineForm> Items { get; set; } = new() { new() };
}

public class SaleLineForm
{
    public string ProductId { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
}

public sealed class SmartInventoryPageModel
{
    public List<SmartInventoryRow> Rows { get; set; } = new();
    public int Alerts => Rows.Count(x => x.Recommendation.IsAtRisk);
    public int Critical => Rows.Count(x => x.Recommendation.IsCritical);
}

public sealed class SmartInventoryRow
{
    public TradingManagementSystem.Application.Services.InventoryRecommendation Recommendation { get; set; } = new();
    public string ProductName { get; set; } = string.Empty;
    public string PointName { get; set; } = string.Empty;
    public string? SourcePointName { get; set; }
}
