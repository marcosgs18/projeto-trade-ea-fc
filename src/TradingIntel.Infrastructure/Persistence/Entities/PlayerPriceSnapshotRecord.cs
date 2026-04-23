namespace TradingIntel.Infrastructure.Persistence.Entities;

internal sealed class PlayerPriceSnapshotRecord
{
    public Guid Id { get; set; }

    public long PlayerId { get; set; }

    public string PlayerDisplayName { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public DateTime CapturedAtUtc { get; set; }

    public decimal BuyNowPrice { get; set; }

    public decimal? SellNowPrice { get; set; }

    public decimal MedianMarketPrice { get; set; }
}
