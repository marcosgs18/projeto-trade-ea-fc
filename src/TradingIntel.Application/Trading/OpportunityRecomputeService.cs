using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingIntel.Application.Persistence;
using TradingIntel.Application.PlayerMarket;
using TradingIntel.Application.Sbc;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;

namespace TradingIntel.Application.Trading;

public sealed class OpportunityRecomputeService : IOpportunityRecomputeService
{
    private static readonly TimeSpan ListingLookback = TimeSpan.FromDays(2);

    private readonly IPlayerPriceSnapshotRepository _priceSnapshots;
    private readonly IMarketListingSnapshotRepository _listingSnapshots;
    private readonly ISbcChallengeRepository _sbcChallenges;
    private readonly IRatingBandDemandService _ratingDemand;
    private readonly ITradeScoringService _tradeScoring;
    private readonly ITradeOpportunityRepository _opportunities;
    private readonly IOptions<OpportunityRecomputeStaleSettings> _staleSettings;
    private readonly IOptions<MarketSourceOptions> _marketSource;
    private readonly ILogger<OpportunityRecomputeService> _logger;

    public OpportunityRecomputeService(
        IPlayerPriceSnapshotRepository priceSnapshots,
        IMarketListingSnapshotRepository listingSnapshots,
        ISbcChallengeRepository sbcChallenges,
        IRatingBandDemandService ratingDemand,
        ITradeScoringService tradeScoring,
        ITradeOpportunityRepository opportunities,
        IOptions<OpportunityRecomputeStaleSettings> staleSettings,
        IOptions<MarketSourceOptions> marketSource,
        ILogger<OpportunityRecomputeService> logger)
    {
        _priceSnapshots = priceSnapshots ?? throw new ArgumentNullException(nameof(priceSnapshots));
        _listingSnapshots = listingSnapshots ?? throw new ArgumentNullException(nameof(listingSnapshots));
        _sbcChallenges = sbcChallenges ?? throw new ArgumentNullException(nameof(sbcChallenges));
        _ratingDemand = ratingDemand ?? throw new ArgumentNullException(nameof(ratingDemand));
        _tradeScoring = tradeScoring ?? throw new ArgumentNullException(nameof(tradeScoring));
        _opportunities = opportunities ?? throw new ArgumentNullException(nameof(opportunities));
        _staleSettings = staleSettings ?? throw new ArgumentNullException(nameof(staleSettings));
        _marketSource = marketSource ?? throw new ArgumentNullException(nameof(marketSource));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OpportunityRecomputeSummary> RecomputeAsync(
        IReadOnlyList<OpportunityRecomputePlayer> players,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(players);

        var nowUtc = DateTime.UtcNow;
        var volatilityWindow = TradeScoringWeights.Default.VolatilityWindow;
        var historyFrom = nowUtc - volatilityWindow;
        var listingFrom = nowUtc - ListingLookback;
        var sourcePrefix = _marketSource.Value.SourcePrefix;

        var challenges = await _sbcChallenges
            .QueryAsync(new SbcChallengeQuery { ActiveAsOfUtc = nowUtc }, cancellationToken)
            .ConfigureAwait(false);

        var demandByBand = _ratingDemand.ComputeDemand(challenges, nowUtc);

        var upserted = 0;
        var removed = 0;
        var skippedOverall = 0;
        var skippedPrice = 0;

        foreach (var entry in players)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.PlayerId <= 0)
            {
                continue;
            }

            if (entry.Overall is not { } overall)
            {
                skippedOverall++;
                _logger.LogInformation(
                    "OpportunityRecompute: skipping player without Overall in watchlist. playerId={PlayerId}",
                    entry.PlayerId);
                continue;
            }

            var displayName = string.IsNullOrWhiteSpace(entry.DisplayName)
                ? $"player-{entry.PlayerId}"
                : entry.DisplayName.Trim();
            var playerRef = new PlayerReference(entry.PlayerId, displayName);

            var currentPrice = await _priceSnapshots
                .GetLatestPriceBySourcePrefixAsync(entry.PlayerId, sourcePrefix, cancellationToken)
                .ConfigureAwait(false);

            if (currentPrice is null)
            {
                skippedPrice++;
                await _opportunities
                    .DeleteByPlayerIdAsync(entry.PlayerId, cancellationToken)
                    .ConfigureAwait(false);
                _logger.LogInformation(
                    "OpportunityRecompute: no price snapshot for source prefix {SourcePrefix}; removed stale row if any. player={Player}",
                    sourcePrefix,
                    playerRef);
                continue;
            }

            var priceHistory = await _priceSnapshots
                .GetPriceHistoryBySourcePrefixAsync(entry.PlayerId, sourcePrefix, historyFrom, nowUtc, cancellationToken)
                .ConfigureAwait(false);

            var recentListings = await _listingSnapshots
                .GetListingsByPlayerBySourcePrefixAsync(entry.PlayerId, sourcePrefix, listingFrom, nowUtc, cancellationToken)
                .ConfigureAwait(false);

            var nearestExpiry = ResolveNearestSbcExpiryUtc(overall, demandByBand, challenges, nowUtc);

            var input = new TradeScoringInput(
                playerRef,
                overall,
                currentPrice,
                priceHistory,
                recentListings,
                demandByBand,
                nowUtc,
                nearestExpiry);

            var scored = _tradeScoring.Score(input);
            if (scored is null)
            {
                await _opportunities
                    .DeleteByPlayerIdAsync(entry.PlayerId, cancellationToken)
                    .ConfigureAwait(false);
                removed++;
                continue;
            }

            await _opportunities
                .UpsertAsync(scored, nowUtc, cancellationToken)
                .ConfigureAwait(false);
            upserted++;
        }

        var staleCutoff = nowUtc - _staleSettings.Value.StaleAfter;
        var staleMarked = await _opportunities
            .MarkStaleWhereLastRecomputedBeforeAsync(staleCutoff, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "OpportunityRecompute: done. upserted={Upserted} removedNoEdge={Removed} skippedOverall={SkipOverall} skippedPrice={SkipPrice} staleMarked={StaleMarked}",
            upserted,
            removed,
            skippedOverall,
            skippedPrice,
            staleMarked);

        return new OpportunityRecomputeSummary(upserted, removed, skippedOverall, skippedPrice, staleMarked);
    }

    internal static DateTime? ResolveNearestSbcExpiryUtc(
        int overallRating,
        IReadOnlyList<RatingBandDemandScore> demandByBand,
        IReadOnlyList<SbcChallenge> challenges,
        DateTime nowUtc)
    {
        RatingBandDemandScore? best = null;
        foreach (var band in demandByBand)
        {
            if (overallRating >= band.Band.FromRating && overallRating <= band.Band.ToRating)
            {
                if (best is null || band.Score > best.Score)
                {
                    best = band;
                }
            }
        }

        if (best is null)
        {
            return null;
        }

        var byId = challenges.ToDictionary(c => c.Id);
        DateTime? minExpiry = null;
        foreach (var id in best.ContributingChallengeIds)
        {
            if (!byId.TryGetValue(id, out var ch))
            {
                continue;
            }

            if (ch.ExpiresAtUtc is not { } exp)
            {
                continue;
            }

            if (exp <= nowUtc)
            {
                continue;
            }

            if (minExpiry is null || exp < minExpiry)
            {
                minExpiry = exp;
            }
        }

        return minExpiry;
    }
}
