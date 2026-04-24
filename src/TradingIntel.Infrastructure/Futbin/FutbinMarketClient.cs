using System.Globalization;
using Microsoft.Extensions.Logging;
using TradingIntel.Application.PlayerMarket;
using TradingIntel.Application.Snapshots;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;

namespace TradingIntel.Infrastructure.Futbin;

/// <summary>
/// Client for the FUTBIN player prices surface (/<year>/playerPrices?player=<id>).
/// The endpoint is protected by Cloudflare, so operational use requires running
/// the client behind an authorized proxy or WAF solver; the adapter intentionally
/// keeps parser and mapper decoupled from transport for testability.
/// </summary>
public sealed class FutbinMarketClient : IPlayerMarketClient
{
    private const string SourceName = "futbin";
    private const string FcYearPath = "26";

    private readonly HttpClient _httpClient;
    private readonly ILogger<FutbinMarketClient> _logger;
    private readonly IRawSnapshotStore _rawSnapshotStore;

    public FutbinMarketClient(HttpClient httpClient, ILogger<FutbinMarketClient> logger, IRawSnapshotStore rawSnapshotStore)
    {
        _httpClient = httpClient;
        _logger = logger;
        _rawSnapshotStore = rawSnapshotStore;
    }

    public async Task<PlayerMarketSnapshot> GetPlayerMarketSnapshotAsync(PlayerReference player, CancellationToken cancellationToken)
    {
        var capturedAtUtc = DateTime.UtcNow;
        var correlationId = Guid.NewGuid().ToString("N");
        var url = $"https://www.futbin.com/{FcYearPath}/playerPrices?player={player.PlayerId.ToString(CultureInfo.InvariantCulture)}";

        string payload;
        try
        {
            payload = await _httpClient.GetStringAsync(url, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Futbin player prices. playerId={PlayerId} url={Url}", player.PlayerId, url);
            throw;
        }

        var metadata = new SourceSnapshotMetadata(
            SourceName,
            capturedAtUtc,
            recordCount: 1,
            correlationId,
            SourceSnapshotMetadata.ComputePayloadHash(payload));

        await _rawSnapshotStore.SaveAsync(metadata, payload, cancellationToken);

        var parser = new FutbinPlayerPricesParser(_logger);
        var parsed = parser.ParsePlayerPrices(payload);

        if (parsed is null)
        {
            return new PlayerMarketSnapshot(
                SourceName,
                capturedAtUtc,
                correlationId,
                payload,
                Array.Empty<PlayerPriceSnapshot>(),
                Array.Empty<MarketListingSnapshot>());
        }

        var mapper = new FutbinPlayerMapper(_logger);
        var prices = mapper.MapPriceSnapshots(parsed, player, capturedAtUtc);
        var listings = mapper.MapLowestListingSnapshots(parsed, player, capturedAtUtc);

        return new PlayerMarketSnapshot(
            SourceName,
            capturedAtUtc,
            correlationId,
            payload,
            prices,
            listings);
    }
}
