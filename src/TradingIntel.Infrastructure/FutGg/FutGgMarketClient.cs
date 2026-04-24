using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingIntel.Application.PlayerMarket;
using TradingIntel.Application.Snapshots;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;

namespace TradingIntel.Infrastructure.FutGg;

/// <summary>
/// FUT.GG market adapter. Calls the public
/// <c>/api/fut/player-prices/&lt;season&gt;/&lt;eaId&gt;/?platform=&lt;p&gt;</c>
/// JSON endpoint once per configured platform, persists the raw payload via
/// <see cref="IRawSnapshotStore"/> and returns a combined
/// <see cref="PlayerMarketSnapshot"/> aggregating prices + live listings from
/// all platforms. The endpoint is not Cloudflare-protected at the JSON layer
/// so a plain HttpClient with a realistic User-Agent is enough (see
/// <c>docs/source-futgg-market.md</c>).
/// </summary>
public sealed class FutGgMarketClient : IPlayerMarketClient
{
    private const string SourceName = "futgg";

    private readonly HttpClient _httpClient;
    private readonly FutGgApiOptions _options;
    private readonly ILogger<FutGgMarketClient> _logger;
    private readonly IRawSnapshotStore _rawSnapshotStore;

    public FutGgMarketClient(
        HttpClient httpClient,
        IOptions<FutGgApiOptions> options,
        ILogger<FutGgMarketClient> logger,
        IRawSnapshotStore rawSnapshotStore)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _rawSnapshotStore = rawSnapshotStore;
    }

    public async Task<PlayerMarketSnapshot> GetPlayerMarketSnapshotAsync(PlayerReference player, CancellationToken cancellationToken)
    {
        var capturedAtUtc = DateTime.UtcNow;
        var correlationId = Guid.NewGuid().ToString("N");

        var platforms = _options.Platforms is { Count: > 0 } configured
            ? configured
            : new List<string> { "pc" };

        var allPrices = new List<PlayerPriceSnapshot>(platforms.Count);
        var allListings = new List<MarketListingSnapshot>();
        var payloads = new List<string>(platforms.Count);

        foreach (var platform in platforms)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedPlatform = (platform ?? "pc").Trim().ToLowerInvariant();
            if (normalizedPlatform.Length == 0)
            {
                continue;
            }

            var url = string.Create(
                CultureInfo.InvariantCulture,
                $"{_options.BaseUrl}/api/fut/player-prices/{_options.Season}/{player.PlayerId}/?platform={normalizedPlatform}");

            string payload;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Accept.ParseAdd("application/json");
                request.Headers.TryAddWithoutValidation("User-Agent", _options.UserAgent);

                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to fetch FUT.GG player prices. playerId={PlayerId} platform={Platform} url={Url}",
                    player.PlayerId, normalizedPlatform, url);
                continue;
            }

            payloads.Add(payload);

            var metadata = new SourceSnapshotMetadata(
                $"{SourceName}:{normalizedPlatform}",
                capturedAtUtc,
                recordCount: 1,
                correlationId,
                SourceSnapshotMetadata.ComputePayloadHash(payload));
            await _rawSnapshotStore.SaveAsync(metadata, payload, cancellationToken).ConfigureAwait(false);

            var parser = new FutGgPlayerPricesParser(_logger);
            var parsed = parser.Parse(payload);
            if (parsed is null)
            {
                continue;
            }

            var mapper = new FutGgPlayerMapper(_logger);
            var priceSnapshot = mapper.MapPriceSnapshot(parsed, player, normalizedPlatform, capturedAtUtc);
            if (priceSnapshot is not null)
            {
                allPrices.Add(priceSnapshot);
            }

            var listings = mapper.MapLiveListings(parsed, player, normalizedPlatform, capturedAtUtc);
            if (listings.Count > 0)
            {
                allListings.AddRange(listings);
            }
        }

        var combinedPayload = payloads.Count == 1
            ? payloads[0]
            : string.Join("\n---\n", payloads);

        return new PlayerMarketSnapshot(
            Source: SourceName,
            CapturedAtUtc: capturedAtUtc,
            CorrelationId: correlationId,
            RawPayload: combinedPayload,
            PriceSnapshots: allPrices,
            LowestListingSnapshots: allListings);
    }
}
