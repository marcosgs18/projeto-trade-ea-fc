namespace TradingIntel.Api.Contracts;

public sealed record PlayerPriceResponse(
    long PlayerId,
    string PlayerDisplayName,
    string Source,
    DateTime CapturedAtUtc,
    decimal BuyNowPrice,
    decimal? SellNowPrice,
    decimal MedianMarketPrice);

public sealed record MarketListingResponse(
    string ListingId,
    long PlayerId,
    string PlayerDisplayName,
    string Source,
    DateTime CapturedAtUtc,
    DateTime ExpiresAtUtc,
    decimal StartingBid,
    decimal BuyNowPrice);
