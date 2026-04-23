using TradingIntel.Domain.Common;
using TradingIntel.Domain.ValueObjects;

namespace TradingIntel.Domain.Models;

public sealed record PlayerPriceSnapshot
{
    public PlayerPriceSnapshot(
        PlayerReference player,
        string source,
        DateTime capturedAtUtc,
        Coins buyNowPrice,
        Coins? sellNowPrice,
        Coins medianMarketPrice)
    {
        Player = player;
        Source = Guard.NotNullOrWhiteSpace(source, nameof(source));
        CapturedAtUtc = Guard.Utc(capturedAtUtc, nameof(capturedAtUtc));

        if (sellNowPrice is not null && sellNowPrice.Value.Value < buyNowPrice.Value)
        {
            throw new ArgumentException("sellNowPrice cannot be less than buyNowPrice.", nameof(sellNowPrice));
        }

        Player = player;
        BuyNowPrice = buyNowPrice;
        SellNowPrice = sellNowPrice;
        MedianMarketPrice = medianMarketPrice;
    }

    public PlayerReference Player { get; }

    public string Source { get; }

    public DateTime CapturedAtUtc { get; }

    public Coins BuyNowPrice { get; }

    public Coins? SellNowPrice { get; }

    public Coins MedianMarketPrice { get; }
}