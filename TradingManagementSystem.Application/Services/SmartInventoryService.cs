using TradingManagementSystem.Domain.Entities;
using TradingManagementSystem.Application.Strategies;

namespace TradingManagementSystem.Application.Services;

/// <summary>Deterministic stock insights; no MongoDB dependency, suitable for unit tests.</summary>
public sealed class SmartInventoryService
{
    public const int HistoryDays = 30;
    public const int PlanningDays = 14;
    private readonly ITargetStockStrategy _targetStrategy;

    // Keep existing tests and consumers source-compatible.
    public SmartInventoryService() : this(new StandardTargetStockStrategy()) { }

    public SmartInventoryService(ITargetStockStrategy targetStrategy)
    {
        _targetStrategy = targetStrategy ?? throw new ArgumentNullException(nameof(targetStrategy));
    }

    public IReadOnlyList<InventoryRecommendation> Analyze(
        IEnumerable<InventoryItem> stock,
        IEnumerable<Sale> sales,
        DateTime asOfUtc)
    {
        ArgumentNullException.ThrowIfNull(stock);
        ArgumentNullException.ThrowIfNull(sales);
        var items = stock.ToList();
        var relevantSales = sales.Where(s => s.IsCompleted &&
            s.CreatedAt >= asOfUtc.AddDays(-HistoryDays) && s.CreatedAt <= asOfUtc).ToList();
        var result = new List<InventoryRecommendation>();

        foreach (var item in items)
        {
            var min = Math.Max(0, item.MinimumQuantity);
            var sold = relevantSales.Where(s => s.TradingPointId == item.TradingPointId)
                .SelectMany(s => s.Items)
                .Where(i => i.ProductId == item.ProductId)
                .Sum(i => Math.Max(0, i.Quantity));
            var daily = (decimal)sold / HistoryDays;
            var target = _targetStrategy.CalculateTarget(min, daily);
            var critical = item.Quantity <= min;
            var atRisk = critical || item.Quantity - daily * PlanningDays < min;
            var needed = atRisk ? Math.Max(0, target - item.Quantity) : 0;

            string? source = null;
            var transfer = 0;
            if (needed > 0)
            {
                foreach (var candidate in items.Where(x => x.ProductId == item.ProductId &&
                    x.TradingPointId != item.TradingPointId))
                {
                    var candidateSold = relevantSales.Where(s => s.TradingPointId == candidate.TradingPointId)
                        .SelectMany(s => s.Items).Where(s => s.ProductId == item.ProductId)
                        .Sum(s => Math.Max(0, s.Quantity));
                    var sourceTarget = _targetStrategy.CalculateTarget(
                        candidate.MinimumQuantity, (decimal)candidateSold / HistoryDays);
                    var surplus = Math.Max(0, candidate.Quantity - sourceTarget);
                    var possible = Math.Min(needed, surplus);
                    if (possible > transfer)
                    {
                        transfer = possible;
                        source = candidate.TradingPointId;
                    }
                }
            }
            result.Add(new InventoryRecommendation
            {
                ProductId = item.ProductId,
                TradingPointId = item.TradingPointId,
                Quantity = item.Quantity,
                MinimumQuantity = min,
                SoldLast30Days = sold,
                AverageDailySales = daily,
                SuggestedTargetQuantity = target,
                IsCritical = critical,
                IsAtRisk = atRisk,
                SuggestedTransferFromId = source,
                SuggestedTransferQuantity = transfer,
                SuggestedPurchaseQuantity = Math.Max(0, needed - transfer)
            });
        }
        return result.OrderByDescending(x => x.IsCritical)
            .ThenByDescending(x => x.IsAtRisk)
            .ThenBy(x => x.Quantity).ToList();
    }
}

public sealed class InventoryRecommendation
{
    public string ProductId { get; set; } = string.Empty;
    public string TradingPointId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int MinimumQuantity { get; set; }
    public int SoldLast30Days { get; set; }
    public decimal AverageDailySales { get; set; }
    public int SuggestedTargetQuantity { get; set; }
    public bool IsCritical { get; set; }
    public bool IsAtRisk { get; set; }
    public string? SuggestedTransferFromId { get; set; }
    public int SuggestedTransferQuantity { get; set; }
    public int SuggestedPurchaseQuantity { get; set; }
}
