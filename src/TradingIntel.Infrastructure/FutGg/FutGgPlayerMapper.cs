using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;

namespace TradingIntel.Infrastructure.FutGg;

/// <summary>
/// Maps a parsed FUT.GG payload (one player, one platform) into domain
/// snapshots. Never throws on partial data — returns empty collections and
/// logs at debug when a field is missing, because upstream fields occasionally
/// disappear for extinct/untradeable cards and the job must keep going.
/// </summary>
public sealed class FutGgPlayerMapper
{
    private readonly ILogger _logger;

    public FutGgPlayerMapper(ILogger logger)
    {
        _logger = logger;
    }

    public PlayerPriceSnapshot? MapPriceSnapshot(
        FutGgPlayerPricesPayload payload,
        PlayerReference player,
        string platform,
        DateTime capturedAtUtc)
    {
        if (payload.CurrentPrice is null)
        {
            _logger.LogDebug(
                "FUT.GG payload has no currentPrice for player {PlayerId} platform {Platform}.",
                player.PlayerId, platform);
            return null;
        }

        if (payload.CurrentPrice.IsUntradeable)
        {
            _logger.LogDebug(
                "FUT.GG reports player {PlayerId} platform {Platform} as untradeable; skipping price snapshot.",
                player.PlayerId, platform);
            return null;
        }

        var buyNow = payload.CurrentPrice.Price;
        if (buyNow <= 0m)
        {
            _logger.LogDebug(
                "FUT.GG price is 0/negative for player {PlayerId} platform {Platform}; skipping.",
                player.PlayerId, platform);
            return null;
        }

        var median = payload.Overview is { AverageBin: > 0m }
            ? payload.Overview.AverageBin
            : buyNow;

        return new PlayerPriceSnapshot(
            player,
            source: BuildSource(platform),
            capturedAtUtc: capturedAtUtc,
            buyNowPrice: new Coins(buyNow),
            sellNowPrice: null,
            medianMarketPrice: new Coins(median));
    }

    public IReadOnlyList<MarketListingSnapshot> MapLiveListings(
        FutGgPlayerPricesPayload payload,
        PlayerReference player,
        string platform,
        DateTime capturedAtUtc)
    {
        if (payload.LiveAuctions.Count == 0)
        {
            return Array.Empty<MarketListingSnapshot>();
        }

        var source = BuildSource(platform);
        var result = new List<MarketListingSnapshot>(payload.LiveAuctions.Count);

        foreach (var auction in payload.LiveAuctions)
        {
            if (auction.BuyNowPrice <= 0m || auction.StartingBid <= 0m)
            {
                continue;
            }

            if (auction.BuyNowPrice < auction.StartingBid)
            {
                continue;
            }

            if (auction.EndDateUtc <= capturedAtUtc)
            {
                continue;
            }

            var listingId = BuildListingId(payload.EaId, platform, auction.EndDateUtc, auction.BuyNowPrice);

            try
            {
                result.Add(new MarketListingSnapshot(
                    listingId: listingId,
                    player: player,
                    source: source,
                    capturedAtUtc: capturedAtUtc,
                    startingBid: new Coins(auction.StartingBid),
                    buyNowPrice: new Coins(auction.BuyNowPrice),
                    expiresAtUtc: auction.EndDateUtc));
            }
            catch (ArgumentException ex)
            {
                _logger.LogDebug(
                    ex,
                    "Skipping FUT.GG auction failing domain guard. player={PlayerId} platform={Platform} bin={Bin} start={Start} end={End}",
                    player.PlayerId, platform, auction.BuyNowPrice, auction.StartingBid, auction.EndDateUtc);
            }
        }

        return result;
    }

    private static string BuildSource(string platform) => $"futgg:{platform}";

    private static string BuildListingId(long eaId, string platform, DateTime endDate, decimal bin)
    {
        var material = $"{eaId}|{platform}|{endDate.Ticks}|{bin.ToString(CultureInfo.InvariantCulture)}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(bytes, 0, 16).ToLowerInvariant();
    }
}
