using TradingIntel.Domain.Common;
using TradingIntel.Domain.ValueObjects;

namespace TradingIntel.Domain.Models;

public sealed record MarketListingSnapshot
{
    public MarketListingSnapshot(
        string listingId,
        PlayerReference player,
        string source,
        DateTime capturedAtUtc,
        Coins startingBid,
        Coins buyNowPrice,
        DateTime expiresAtUtc)
    {
        ListingId = Guard.NotNullOrWhiteSpace(listingId, nameof(listingId));
        Player = player;
        Source = Guard.NotNullOrWhiteSpace(source, nameof(source));
        CapturedAtUtc = Guard.Utc(capturedAtUtc, nameof(capturedAtUtc));
        ExpiresAtUtc = Guard.Utc(expiresAtUtc, nameof(expiresAtUtc));

        if (expiresAtUtc <= capturedAtUtc)
        {
            throw new ArgumentException("expiresAtUtc must be greater than capturedAtUtc.", nameof(expiresAtUtc));
        }

        if (buyNowPrice.Value < startingBid.Value)
        {
            throw new ArgumentException("buyNowPrice cannot be less than startingBid.", nameof(buyNowPrice));
        }

        StartingBid = startingBid;
        BuyNowPrice = buyNowPrice;
    }

    public string ListingId { get; }

    public PlayerReference Player { get; }

    public string Source { get; }

    public DateTime CapturedAtUtc { get; }

    public Coins StartingBid { get; }

    public Coins BuyNowPrice { get; }

    public DateTime ExpiresAtUtc { get; }
}