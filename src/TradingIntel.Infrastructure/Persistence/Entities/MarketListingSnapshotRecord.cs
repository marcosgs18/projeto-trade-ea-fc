namespace TradingIntel.Infrastructure.Persistence.Entities;

internal sealed class MarketListingSnapshotRecord
{
    public Guid Id { get; set; }

    public string ListingId { get; set; } = string.Empty;

    public long PlayerId { get; set; }

    public string PlayerDisplayName { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public DateTime CapturedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public decimal StartingBid { get; set; }

    public decimal BuyNowPrice { get; set; }
}
