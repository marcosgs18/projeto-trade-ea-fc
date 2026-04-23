using TradingIntel.Domain.Common;
using TradingIntel.Domain.ValueObjects;

namespace TradingIntel.Domain.Models;

public sealed record PortfolioPosition
{
    public PortfolioPosition(PlayerReference player, int quantity, Coins averageAcquisitionPrice, DateTime updatedAtUtc)
    {
        Player = player;
        Quantity = Guard.Positive(quantity, nameof(quantity));
        AverageAcquisitionPrice = averageAcquisitionPrice;
        UpdatedAtUtc = Guard.Utc(updatedAtUtc, nameof(updatedAtUtc));
    }

    public PlayerReference Player { get; }

    public int Quantity { get; }

    public Coins AverageAcquisitionPrice { get; }

    public DateTime UpdatedAtUtc { get; }

    public Coins EstimatePositionValue(Coins marketPrice)
    {
        return new Coins(marketPrice.Value * Quantity);
    }

    public Coins EstimateUnrealizedProfit(Coins marketPrice)
    {
        var currentTotal = marketPrice.Value * Quantity;
        var costTotal = AverageAcquisitionPrice.Value * Quantity;
        return new Coins(Math.Max(0, currentTotal - costTotal));
    }
}