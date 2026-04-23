using Microsoft.Extensions.Logging;
using TradingIntel.Application.FutGg;
using TradingIntel.Application.Snapshots;
using TradingIntel.Domain.Models;

namespace TradingIntel.Infrastructure.FutGg;

/// <summary>
/// FUT.GG is a JS-rendered site; to keep parsing stable and testable with HTML fixtures,
/// we fetch a public, rendered/text representation via r.jina.ai.
/// </summary>
public sealed class FutGgSbcClient : IFutGgSbcClient
{
    private const string SourceName = "futgg";

    private readonly HttpClient _httpClient;
    private readonly ILogger<FutGgSbcClient> _logger;
    private readonly IRawSnapshotStore _rawSnapshotStore;

    public FutGgSbcClient(HttpClient httpClient, ILogger<FutGgSbcClient> logger, IRawSnapshotStore rawSnapshotStore)
    {
        _httpClient = httpClient;
        _logger = logger;
        _rawSnapshotStore = rawSnapshotStore;
    }

    public async Task<FutGgSbcListingSnapshot> GetSbcListingSnapshotAsync(CancellationToken cancellationToken)
    {
        var capturedAtUtc = DateTime.UtcNow;
        var correlationId = Guid.NewGuid().ToString("N");

        // Rendered listing content (public) used as a stable parsing input.
        var listingUrl = "https://r.jina.ai/https://www.fut.gg/sbc/";

        string payload;
        try
        {
            payload = await _httpClient.GetStringAsync(listingUrl, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch FUT.GG SBC listing. url={Url}", listingUrl);
            throw;
        }

        var hash = SourceSnapshotMetadata.ComputePayloadHash(payload);
        var metadata = new SourceSnapshotMetadata(SourceName, capturedAtUtc, recordCount: 1, correlationId, hash);
        await _rawSnapshotStore.SaveAsync(metadata, payload, cancellationToken);

        var parser = new FutGgSbcListingParser(_logger);
        var parsed = parser.ParseListing(payload, capturedAtUtc);

        var detailParser = new FutGgSbcDetailParser(_logger);
        var enrichedItems = new List<FutGgSbcListingItemParsed>(parsed.Count);

        foreach (var item in parsed)
        {
            var detailPayload = await TryGetDetailPayloadAsync(item.DetailsUrl, cancellationToken);
            if (detailPayload is null)
            {
                enrichedItems.Add(item);
                continue;
            }

            var requirementLines = detailParser.ParseVisibleRequirements(detailPayload);
            enrichedItems.Add(item with { RequirementLines = requirementLines });
        }

        var mapper = new FutGgSbcMapper(_logger);
        var challenges = enrichedItems.Select(item => mapper.Map(item, capturedAtUtc)).ToArray();

        return new FutGgSbcListingSnapshot(SourceName, capturedAtUtc, correlationId, payload, challenges);
    }

    private async Task<string?> TryGetDetailPayloadAsync(string detailsUrl, CancellationToken cancellationToken)
    {
        var renderedUrl = $"https://r.jina.ai/{detailsUrl}";

        try
        {
            var payload = await _httpClient.GetStringAsync(renderedUrl, cancellationToken);

            var metadata = new SourceSnapshotMetadata(
                SourceName,
                DateTime.UtcNow,
                recordCount: 1,
                correlationId: Guid.NewGuid().ToString("N"),
                payloadHash: SourceSnapshotMetadata.ComputePayloadHash(payload));

            await _rawSnapshotStore.SaveAsync(metadata, payload, cancellationToken);
            return payload;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch FUT.GG SBC detail. url={Url}", detailsUrl);
            return null;
        }
    }
}

