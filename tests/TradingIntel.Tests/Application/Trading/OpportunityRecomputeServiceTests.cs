using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradingIntel.Application.Persistence;
using TradingIntel.Application.Sbc;
using TradingIntel.Application.Trading;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;
using Xunit;

namespace TradingIntel.Tests.Application.Trading;

public sealed class OpportunityRecomputeServiceTests
{
    private static readonly DateTime NowUtc = new(2026, 04, 22, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ResolveNearestSbcExpiryUtc_returns_minimum_future_expiry_from_contributing_challenges()
    {
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var band = new RatingBandDemandScore(
            new RatingBand(84, 86),
            score: 0.5,
            totalRequiredCards: 3,
            new[] { idA, idB },
            reasons: Array.Empty<DemandReason>());

        var challenges = new[]
        {
            new SbcChallenge(
                idA,
                "A",
                "players",
                NowUtc.AddDays(2),
                SbcRepeatability.NotRepeatable(),
                "set",
                NowUtc,
                new[] { new SbcRequirement("min_overall_84", 84, null) }),
            new SbcChallenge(
                idB,
                "B",
                "players",
                NowUtc.AddHours(12),
                SbcRepeatability.NotRepeatable(),
                "set",
                NowUtc,
                new[] { new SbcRequirement("min_overall_84", 84, null) }),
        };

        var nearest = OpportunityRecomputeService.ResolveNearestSbcExpiryUtc(84, new[] { band }, challenges, NowUtc);

        nearest.Should().Be(NowUtc.AddHours(12));
    }

    [Fact]
    public async Task RecomputeAsync_calls_delete_when_scoring_returns_null()
    {
        var player = new PlayerReference(9_001, "Edge Case");
        var current = new PlayerPriceSnapshot(
            player,
            "futbin:ps",
            NowUtc,
            new Coins(10_000),
            null,
            new Coins(12_000));

        var priceRepo = new StubPlayerPriceRepo { LatestFutbin = current };
        var listingRepo = new StubListingRepo();
        var sbcRepo = new StubSbcRepo { Challenges = Array.Empty<SbcChallenge>() };
        var demand = new RatingBandDemandService(NullLogger<RatingBandDemandService>.Instance);
        var scoring = new StubTradeScoring { Result = null };
        var oppRepo = new CaptureTradeOpportunityRepository();

        var svc = new OpportunityRecomputeService(
            priceRepo,
            listingRepo,
            sbcRepo,
            demand,
            scoring,
            oppRepo,
            Options.Create(new OpportunityRecomputeStaleSettings { StaleAfter = TimeSpan.FromHours(1) }),
            NullLogger<OpportunityRecomputeService>.Instance);

        var summary = await svc.RecomputeAsync(
            new[] { new OpportunityRecomputePlayer(9_001, "Edge Case", 84) },
            CancellationToken.None);

        summary.RemovedNoEdge.Should().Be(1);
        oppRepo.DeletedPlayerIds.Should().ContainSingle().Which.Should().Be(9_001);
        oppRepo.Upserted.Should().BeEmpty();
    }

    [Fact]
    public async Task RecomputeAsync_upserts_when_scoring_returns_opportunity()
    {
        var player = new PlayerReference(9_002, "Winner");
        var current = new PlayerPriceSnapshot(
            player,
            "futbin:ps",
            NowUtc,
            new Coins(10_000),
            null,
            new Coins(12_000));

        var oppId = Guid.NewGuid();
        var opportunity = new TradeOpportunity(
            oppId,
            player,
            NowUtc,
            new Coins(10_000),
            new Coins(12_000),
            new ConfidenceScore(0.75m),
            new[]
            {
                new OpportunityReason("TRADE_SCORE", "ok", 0.75m),
            },
            new[]
            {
                new ExecutionSuggestion(Guid.NewGuid(), oppId, ExecutionAction.Buy, new Coins(10_000), NowUtc.AddMinutes(15)),
                new ExecutionSuggestion(Guid.NewGuid(), oppId, ExecutionAction.ListForSale, new Coins(12_000), NowUtc.AddHours(24)),
            });

        var priceRepo = new StubPlayerPriceRepo { LatestFutbin = current };
        var listingRepo = new StubListingRepo();
        var sbcRepo = new StubSbcRepo { Challenges = Array.Empty<SbcChallenge>() };
        var demand = new RatingBandDemandService(NullLogger<RatingBandDemandService>.Instance);
        var scoring = new StubTradeScoring { Result = opportunity };
        var oppRepo = new CaptureTradeOpportunityRepository();

        var svc = new OpportunityRecomputeService(
            priceRepo,
            listingRepo,
            sbcRepo,
            demand,
            scoring,
            oppRepo,
            Options.Create(new OpportunityRecomputeStaleSettings { StaleAfter = TimeSpan.FromHours(1) }),
            NullLogger<OpportunityRecomputeService>.Instance);

        var summary = await svc.RecomputeAsync(
            new[] { new OpportunityRecomputePlayer(9_002, "Winner", 84) },
            CancellationToken.None);

        summary.Upserted.Should().Be(1);
        oppRepo.Upserted.Should().ContainSingle();
        oppRepo.Upserted[0].Player.PlayerId.Should().Be(9_002);
        oppRepo.DeletedPlayerIds.Should().BeEmpty();
    }

    private sealed class StubPlayerPriceRepo : IPlayerPriceSnapshotRepository
    {
        public PlayerPriceSnapshot? LatestFutbin { get; set; }

        public Task AddRangeAsync(IEnumerable<PlayerPriceSnapshot> snapshots, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<PlayerPriceSnapshot>> GetByPlayerAsync(
            long playerId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PlayerPriceSnapshot>>(Array.Empty<PlayerPriceSnapshot>());

        public Task<PlayerPriceSnapshot?> GetLatestForPlayerAsync(
            long playerId,
            string source,
            CancellationToken cancellationToken) =>
            Task.FromResult<PlayerPriceSnapshot?>(null);

        public Task<PlayerPriceSnapshot?> GetLatestFutbinPriceForPlayerAsync(
            long playerId,
            CancellationToken cancellationToken) =>
            Task.FromResult(LatestFutbin);

    public Task<IReadOnlyList<PlayerPriceSnapshot>> GetFutbinPriceHistoryAsync(
        long playerId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PlayerPriceSnapshot>>(Array.Empty<PlayerPriceSnapshot>());

        public Task<(IReadOnlyList<PlayerPriceSnapshot> Items, int TotalCount)> GetByPlayerPagedAsync(
            long playerId,
            string? source,
            DateTime fromUtc,
            DateTime toUtc,
            int skip,
            int take,
            CancellationToken cancellationToken) =>
            Task.FromResult<(IReadOnlyList<PlayerPriceSnapshot> Items, int TotalCount)>((Array.Empty<PlayerPriceSnapshot>(), 0));
    }

    private sealed class StubListingRepo : IMarketListingSnapshotRepository
    {
        public Task AddRangeAsync(IEnumerable<MarketListingSnapshot> snapshots, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<MarketListingSnapshot>> GetByPlayerAsync(
            long playerId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MarketListingSnapshot>>(Array.Empty<MarketListingSnapshot>());

        public Task<MarketListingSnapshot?> GetByListingIdAsync(string listingId, CancellationToken cancellationToken) =>
            Task.FromResult<MarketListingSnapshot?>(null);

    public Task<IReadOnlyList<MarketListingSnapshot>> GetFutbinListingsByPlayerAsync(
        long playerId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MarketListingSnapshot>>(Array.Empty<MarketListingSnapshot>());

        public Task<(IReadOnlyList<MarketListingSnapshot> Items, int TotalCount)> GetByPlayerPagedAsync(
            long playerId,
            DateTime fromUtc,
            DateTime toUtc,
            int skip,
            int take,
            CancellationToken cancellationToken) =>
            Task.FromResult<(IReadOnlyList<MarketListingSnapshot> Items, int TotalCount)>((Array.Empty<MarketListingSnapshot>(), 0));
    }

    private sealed class StubSbcRepo : ISbcChallengeRepository
    {
        public IReadOnlyList<SbcChallenge> Challenges { get; set; } = Array.Empty<SbcChallenge>();

        public Task UpsertRangeAsync(IEnumerable<SbcChallenge> challenges, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<SbcChallenge?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<SbcChallenge?>(null);

        public Task<IReadOnlyList<SbcChallenge>> QueryAsync(
            SbcChallengeQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult(Challenges);

        public Task<(IReadOnlyList<SbcChallenge> Items, int TotalCount)> QueryActivePagedAsync(
            SbcActiveListQuery query,
            int skip,
            int take,
            CancellationToken cancellationToken) =>
            Task.FromResult<(IReadOnlyList<SbcChallenge> Items, int TotalCount)>((Challenges, Challenges.Count));
    }

    private sealed class StubTradeScoring : ITradeScoringService
    {
        public TradeOpportunity? Result { get; set; }

        public TradeOpportunity? Score(TradeScoringInput input, TradeScoringWeights? weights = null) => Result;
    }

    private sealed class CaptureTradeOpportunityRepository : ITradeOpportunityRepository
    {
        public List<long> DeletedPlayerIds { get; } = new();

        public List<TradeOpportunity> Upserted { get; } = new();

        public Task UpsertAsync(
            TradeOpportunity opportunity,
            DateTime lastRecomputedAtUtc,
            CancellationToken cancellationToken)
        {
            Upserted.Add(opportunity);
            return Task.CompletedTask;
        }

        public Task DeleteByPlayerIdAsync(long playerId, CancellationToken cancellationToken)
        {
            DeletedPlayerIds.Add(playerId);
            return Task.CompletedTask;
        }

        public Task<int> MarkStaleWhereLastRecomputedBeforeAsync(
            DateTime cutoffUtc,
            CancellationToken cancellationToken) =>
            Task.FromResult(0);

        public Task<bool> ExistsForPlayerAsync(long playerId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<(IReadOnlyList<TradeOpportunityStoredView> Items, int TotalCount)> QueryPagedAsync(
            TradeOpportunityListFilter filter,
            int skip,
            int take,
            CancellationToken cancellationToken) =>
            Task.FromResult<(IReadOnlyList<TradeOpportunityStoredView> Items, int TotalCount)>(
                (Array.Empty<TradeOpportunityStoredView>(), 0));

        public Task<TradeOpportunityStoredView?> GetByOpportunityIdAsync(
            Guid opportunityId,
            CancellationToken cancellationToken) =>
            Task.FromResult<TradeOpportunityStoredView?>(null);
    }
}
