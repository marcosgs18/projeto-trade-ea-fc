using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;

namespace TradingIntel.Infrastructure.Futbin;

public sealed class FutbinPlayerMapper
{
    private const string SourceName = "futbin";
    private static readonly TimeSpan LowestListingValidity = TimeSpan.FromMinutes(10);

    private readonly ILogger _logger;

    public FutbinPlayerMapper(ILogger logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<PlayerPriceSnapshot> MapPriceSnapshots(
        FutbinPlayerPricesParsed parsed,
        PlayerReference player,
        DateTime capturedAtUtc)
    {
        var results = new List<PlayerPriceSnapshot>();

        foreach (var platform in parsed.Platforms)
        {
            if (platform.LowestBinPrice is null)
            {
                continue;
            }

            try
            {
                var buy = new Coins(platform.LowestBinPrice.Value);
                var median = ComputeMedian(platform);
                var sell = platform.SecondLowestBinPrice is null ? (Coins?)null : new Coins(Math.Max(platform.SecondLowestBinPrice.Value, platform.LowestBinPrice.Value));

                var snapshot = new PlayerPriceSnapshot(
                    player,
                    $"{SourceName}:{platform.Platform}",
                    capturedAtUtc,
                    buy,
                    sell,
                    median);

                results.Add(snapshot);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to map Futbin price snapshot. playerId={PlayerId} platform={Platform}",
                    parsed.PlayerId,
                    platform.Platform);
            }
        }

        return results;
    }

    public IReadOnlyList<MarketListingSnapshot> MapLowestListingSnapshots(
        FutbinPlayerPricesParsed parsed,
        PlayerReference player,
        DateTime capturedAtUtc)
    {
        var results = new List<MarketListingSnapshot>();

        foreach (var platform in parsed.Platforms)
        {
            if (platform.LowestBinPrice is null)
            {
                continue;
            }

            try
            {
                var listingId = BuildListingId(parsed.PlayerId, platform.Platform, capturedAtUtc);
                var price = new Coins(platform.LowestBinPrice.Value);
                var snapshot = new MarketListingSnapshot(
                    listingId,
                    player,
                    $"{SourceName}:{platform.Platform}",
                    capturedAtUtc,
                    startingBid: price,
                    buyNowPrice: price,
                    expiresAtUtc: capturedAtUtc.Add(LowestListingValidity));

                results.Add(snapshot);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to map Futbin listing snapshot. playerId={PlayerId} platform={Platform}",
                    parsed.PlayerId,
                    platform.Platform);
            }
        }

        return results;
    }

    private static Coins ComputeMedian(FutbinPlatformPriceParsed platform)
    {
        var candidates = new[] { platform.LowestBinPrice, platform.SecondLowestBinPrice, platform.MinPriceRange, platform.MaxPriceRange }
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .OrderBy(value => value)
            .ToArray();

        if (candidates.Length == 0)
        {
            return new Coins(platform.LowestBinPrice ?? 0);
        }

        var middle = candidates.Length / 2;
        var median = candidates.Length % 2 == 0
            ? (candidates[middle - 1] + candidates[middle]) / 2m
            : candidates[middle];

        return new Coins(median);
    }

    private static string BuildListingId(long playerId, string platform, DateTime capturedAtUtc)
    {
        var raw = $"{playerId}:{platform}:{capturedAtUtc.ToBinary()}";
        var bytes = Encoding.UTF8.GetBytes(raw);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
