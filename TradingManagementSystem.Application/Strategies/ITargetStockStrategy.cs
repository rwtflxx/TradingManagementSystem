namespace TradingManagementSystem.Application.Strategies;

/// <summary>Strategy: determines the desired stock quantity for one trading point.</summary>
public interface ITargetStockStrategy
{
    int CalculateTarget(int minimumQuantity, decimal averageDailySales);
}

/// <summary>Original StockFlow rule: max(2 * minimum, forecast demand + minimum).</summary>
public sealed class StandardTargetStockStrategy : ITargetStockStrategy
{
    public const int DefaultPlanningDays = 14;
    private readonly int _planningDays;

    public StandardTargetStockStrategy(int planningDays = DefaultPlanningDays)
    {
        if (planningDays <= 0)
            throw new ArgumentOutOfRangeException(nameof(planningDays));
        _planningDays = planningDays;
    }

    public int CalculateTarget(int minimumQuantity, decimal averageDailySales)
    {
        var min = Math.Max(0, minimumQuantity);
        return Math.Max(min * 2,
            checked((int)Math.Ceiling(averageDailySales * _planningDays)) + min);
    }
}

/// <summary>Alternative rule: increases forecast demand by a seasonal factor.</summary>
public sealed class SeasonalTargetStockStrategy : ITargetStockStrategy
{
    private readonly StandardTargetStockStrategy _standard;
    private readonly decimal _multiplier;

    public SeasonalTargetStockStrategy(decimal multiplier,
        int planningDays = StandardTargetStockStrategy.DefaultPlanningDays)
    {
        if (multiplier <= 0)
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        _standard = new StandardTargetStockStrategy(planningDays);
        _multiplier = multiplier;
    }

    public int CalculateTarget(int minimumQuantity, decimal averageDailySales)
        => _standard.CalculateTarget(minimumQuantity, averageDailySales * _multiplier);
}
