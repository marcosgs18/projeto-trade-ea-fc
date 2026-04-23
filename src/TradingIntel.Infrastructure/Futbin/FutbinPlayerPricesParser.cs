using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TradingIntel.Infrastructure.Futbin;

public sealed record FutbinPlatformPriceParsed(
    string Platform,
    decimal? LowestBinPrice,
    decimal? SecondLowestBinPrice,
    decimal? MinPriceRange,
    decimal? MaxPriceRange,
    int? RecentPercent,
    string? Updated);

public sealed record FutbinPlayerPricesParsed(
    long PlayerId,
    IReadOnlyList<FutbinPlatformPriceParsed> Platforms);

public sealed class FutbinPlayerPricesParser
{
    private static readonly string[] KnownPlatforms = { "ps", "xbox", "pc" };

    private readonly ILogger _logger;

    public FutbinPlayerPricesParser(ILogger logger)
    {
        _logger = logger;
    }

    public FutbinPlayerPricesParsed? ParsePlayerPrices(string jsonPayload)
    {
        if (string.IsNullOrWhiteSpace(jsonPayload))
        {
            _logger.LogWarning("Futbin player prices payload is empty.");
            return null;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(jsonPayload);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Futbin player prices payload is not valid JSON. length={Length}", jsonPayload.Length);
            return null;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                _logger.LogWarning("Futbin player prices payload root is not an object. kind={Kind}", document.RootElement.ValueKind);
                return null;
            }

            foreach (var entry in document.RootElement.EnumerateObject())
            {
                if (!long.TryParse(entry.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var playerId))
                {
                    continue;
                }

                if (!entry.Value.TryGetProperty("prices", out var prices) || prices.ValueKind != JsonValueKind.Object)
                {
                    _logger.LogWarning("Futbin player prices missing prices object. playerId={PlayerId}", playerId);
                    continue;
                }

                var platforms = new List<FutbinPlatformPriceParsed>();
                foreach (var platformName in KnownPlatforms)
                {
                    if (!prices.TryGetProperty(platformName, out var platformElement) || platformElement.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    platforms.Add(ParsePlatform(platformName, platformElement, playerId));
                }

                return new FutbinPlayerPricesParsed(playerId, platforms);
            }

            _logger.LogWarning("Futbin player prices payload did not contain any player entry.");
            return null;
        }
    }

    private FutbinPlatformPriceParsed ParsePlatform(string platform, JsonElement element, long playerId)
    {
        var lc = TryParseCoins(element, "LCPrice");
        var lc2 = TryParseCoins(element, "LCPrice2");
        var min = TryParseCoins(element, "MinPrice");
        var max = TryParseCoins(element, "MaxPrice");
        var prp = TryParseInt(element, "PRP");
        var updated = TryReadString(element, "updated");

        if (lc is null)
        {
            _logger.LogWarning(
                "Futbin platform missing LCPrice. playerId={PlayerId} platform={Platform}",
                playerId,
                platform);
        }

        return new FutbinPlatformPriceParsed(
            platform,
            lc,
            lc2,
            min,
            max,
            prp,
            updated);
    }

    private static decimal? TryParseCoins(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var raw = (value.GetString() ?? string.Empty).Replace(",", string.Empty).Trim();
        if (raw.Length == 0)
        {
            return null;
        }

        if (!decimal.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return null;
        }

        return parsed <= 0 ? null : parsed;
    }

    private static int? TryParseInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null,
        };
    }

    private static string? TryReadString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var raw = value.GetString();
        return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
    }
}
