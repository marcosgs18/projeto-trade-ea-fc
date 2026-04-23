using TradingIntel.Api.Contracts;
using TradingIntel.Domain.Models;

namespace TradingIntel.Api.Mapping;

internal static class MarketMapper
{
    public static PlayerPriceResponse ToResponse(PlayerPriceSnapshot s) =>
        new(
            s.Player.PlayerId,
            s.Player.DisplayName,
            s.Source,
            s.CapturedAtUtc,
            s.BuyNowPrice.Value,
            s.SellNowPrice?.Value,
            s.MedianMarketPrice.Value);

    public static MarketListingResponse ToResponse(MarketListingSnapshot s) =>
        new(
            s.ListingId,
            s.Player.PlayerId,
            s.Player.DisplayName,
            s.Source,
            s.CapturedAtUtc,
            s.ExpiresAtUtc,
            s.StartingBid.Value,
            s.BuyNowPrice.Value);
}
