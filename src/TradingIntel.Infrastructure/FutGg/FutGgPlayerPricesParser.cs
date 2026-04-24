using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace TradingIntel.Infrastructure.FutGg;

/// <summary>
/// Parses the FUT.GG <c>/api/fut/player-prices/&lt;season&gt;/&lt;eaId&gt;/?platform=&lt;p&gt;</c>
/// JSON response into a transport-agnostic DTO. The mapper converts that DTO
/// into domain snapshots; splitting parser from mapper keeps each concern
/// unit-testable against a real captured payload.
/// </summary>
public sealed class FutGgPlayerPricesParser
{
    private readonly ILogger _logger;

    public FutGgPlayerPricesParser(ILogger logger)
    {
        _logger = logger;
    }

    public FutGgPlayerPricesPayload? Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            {
                _logger.LogWarning("FUT.GG response missing 'data' object.");
                return null;
            }

            var eaId = data.TryGetProperty("eaId", out var eaIdEl) && eaIdEl.TryGetInt64(out var id) ? id : 0L;

            var currentPrice = ReadCurrentPrice(data);
            var liveAuctions = ReadLiveAuctions(data);
            var overview = ReadOverview(data);

            return new FutGgPlayerPricesPayload(eaId, currentPrice, liveAuctions, overview);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse FUT.GG player prices JSON.");
            return null;
        }
    }

    private static FutGgCurrentPrice? ReadCurrentPrice(JsonElement data)
    {
        if (!data.TryGetProperty("currentPrice", out var cp) || cp.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var price = cp.TryGetProperty("price", out var pEl) && pEl.TryGetDecimal(out var p) ? p : 0m;
        var platform = cp.TryGetProperty("platform", out var plEl) && plEl.ValueKind == JsonValueKind.String
            ? plEl.GetString() ?? string.Empty
            : string.Empty;
        var isExtinct = cp.TryGetProperty("isExtinct", out var exEl) && exEl.ValueKind == JsonValueKind.True;
        var isUntradeable = cp.TryGetProperty("isUntradeable", out var unEl) && unEl.ValueKind == JsonValueKind.True;
        var priceUpdatedAt = cp.TryGetProperty("priceUpdatedAt", out var puEl) && puEl.ValueKind == JsonValueKind.String
            ? TryParseUtc(puEl.GetString())
            : null;

        return new FutGgCurrentPrice(price, platform, isExtinct, isUntradeable, priceUpdatedAt);
    }

    private static IReadOnlyList<FutGgLiveAuction> ReadLiveAuctions(JsonElement data)
    {
        if (!data.TryGetProperty("liveAuctions", out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<FutGgLiveAuction>();
        }

        var list = new List<FutGgLiveAuction>(arr.GetArrayLength());
        foreach (var el in arr.EnumerateArray())
        {
            var bin = el.TryGetProperty("buyNowPrice", out var bEl) && bEl.TryGetDecimal(out var b) ? b : 0m;
            var start = el.TryGetProperty("startingBid", out var sEl) && sEl.TryGetDecimal(out var s) ? s : 0m;
            var end = el.TryGetProperty("endDate", out var eEl) && eEl.ValueKind == JsonValueKind.String
                ? TryParseUtc(eEl.GetString())
                : null;

            if (end is null)
            {
                continue;
            }

            list.Add(new FutGgLiveAuction(bin, start, end.Value));
        }

        return list;
    }

    private static FutGgOverview? ReadOverview(JsonElement data)
    {
        if (!data.TryGetProperty("overview", out var ov) || ov.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var avg = ov.TryGetProperty("averageBin", out var aEl) && aEl.TryGetDecimal(out var a) ? a : 0m;
        var cheapest = ov.TryGetProperty("cheapestSale", out var cEl) && cEl.TryGetDecimal(out var c) ? c : 0m;
        var yAvg = ov.TryGetProperty("yesterdayAverageBin", out var yEl) && yEl.TryGetDecimal(out var y) ? y : 0m;

        return new FutGgOverview(avg, cheapest, yAvg);
    }

    private static DateTime? TryParseUtc(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParse(
                value,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                out var parsed))
        {
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }

        return null;
    }
}

public sealed record FutGgPlayerPricesPayload(
    long EaId,
    FutGgCurrentPrice? CurrentPrice,
    IReadOnlyList<FutGgLiveAuction> LiveAuctions,
    FutGgOverview? Overview);

public sealed record FutGgCurrentPrice(
    decimal Price,
    string Platform,
    bool IsExtinct,
    bool IsUntradeable,
    DateTime? PriceUpdatedAt);

public sealed record FutGgLiveAuction(
    decimal BuyNowPrice,
    decimal StartingBid,
    DateTime EndDateUtc);

public sealed record FutGgOverview(
    decimal AverageBin,
    decimal CheapestSale,
    decimal YesterdayAverageBin);
