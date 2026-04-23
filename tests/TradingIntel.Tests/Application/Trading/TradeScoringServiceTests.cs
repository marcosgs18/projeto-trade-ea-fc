using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TradingIntel.Application.Sbc;
using TradingIntel.Application.Trading;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;
using Xunit;

namespace TradingIntel.Tests.Application.Trading;

public sealed class TradeScoringServiceTests
{
    private static readonly DateTime NowUtc = new(2026, 04, 22, 12, 0, 0, DateTimeKind.Utc);
    private static readonly PlayerReference Player = new(123_456, "Synthetic Striker");

    [Fact]
    public void Score_returns_null_when_net_margin_is_not_positive()
    {
        var service = CreateService();

        // buy=1000, sell=1050 → netSell = floor(1050*0.95) = 997 → margem = -3
        var input = BuildInput(
            buyNow: 1000,
            sellNow: null,
            median: 1050,
            demandByBand: Array.Empty<RatingBandDemandScore>(),
            listings: 5,
            history: PriceSeriesAround(1000, 0.01, 6));

        var result = service.Score(input);

        result.Should().BeNull();
    }

    [Fact]
    public void Score_returns_null_when_prices_are_equal()
    {
        var service = CreateService();
        var input = BuildInput(
            buyNow: 5000,
            sellNow: 5000,
            median: 5000);

        service.Score(input).Should().BeNull();
    }

    [Fact]
    public void Score_returns_opportunity_with_net_margin_when_edge_exists()
    {
        var service = CreateService();

        // buy=10_000, sell=12_000 → netSell = floor(12000 * 0.95) = 11400 → margem = 1400
        var input = BuildInput(
            buyNow: 10_000,
            sellNow: null,
            median: 12_000,
            demandByBand: new[] { BandScore(from: 83, to: 85, score: 0.8) },
            overallRating: 84,
            listings: 20,
            history: PriceSeriesAround(10_000, 0.02, 10),
            nearestSbcExpiry: NowUtc.AddHours(20));

        var result = service.Score(input);

        result.Should().NotBeNull();
        result!.ExpectedBuyPrice.Value.Should().Be(10_000);
        result.ExpectedSellPrice.Value.Should().Be(12_000);
        result.ExpectedProfit.Value.Should().Be(2000); // gross
        result.ExpectedNetMargin.Value.Should().Be(1400); // after 5% tax
        result.Confidence.Value.Should().BeInRange(0m, 1m);
        result.Reasons.Should().Contain(r => r.Code == "TRADE_SCORE");
        result.Reasons.Should().Contain(r => r.Code == "DEMAND_OVERALL");
        result.Reasons.Should().Contain(r => r.Code == "NET_SPREAD");
        result.Reasons.Should().Contain(r => r.Code == "MARKET_LIQUIDITY");
        result.Reasons.Should().Contain(r => r.Code == "PRICE_STABILITY");
        result.Reasons.Should().Contain(r => r.Code == "SBC_EXPIRY_WINDOW");
        result.Reasons.Should().OnlyContain(r => r.Weight >= 0m && r.Weight <= 1m);
        result.Suggestions.Should().Contain(s => s.Action == ExecutionAction.Buy);
        result.Suggestions.Should().Contain(s => s.Action == ExecutionAction.ListForSale);
    }

    [Fact]
    public void Score_prefers_SellNowPrice_when_provided()
    {
        var service = CreateService();

        // sellNow (2000) deve ser usado no lugar de median (1500)
        var input = BuildInput(
            buyNow: 1000,
            sellNow: 2000,
            median: 1500);

        var result = service.Score(input);

        result.Should().NotBeNull();
        result!.ExpectedSellPrice.Value.Should().Be(2000);
    }

    [Fact]
    public void Score_high_demand_raises_confidence()
    {
        var service = CreateService();

        var high = BuildInput(
            buyNow: 10_000, sellNow: null, median: 12_000,
            demandByBand: new[] { BandScore(84, 86, 1.0) },
            overallRating: 84,
            listings: 20,
            history: PriceSeriesAround(10_000, 0.02, 10),
            nearestSbcExpiry: NowUtc.AddHours(20));

        var low = BuildInput(
            buyNow: 10_000, sellNow: null, median: 12_000,
            demandByBand: new[] { BandScore(84, 86, 0.05) },
            overallRating: 84,
            listings: 20,
            history: PriceSeriesAround(10_000, 0.02, 10),
            nearestSbcExpiry: NowUtc.AddHours(20));

        service.Score(high)!.Confidence.Value
            .Should().BeGreaterThan(service.Score(low)!.Confidence.Value);
    }

    [Fact]
    public void Score_high_liquidity_raises_confidence()
    {
        var service = CreateService();

        var liquid = BuildInput(listings: 30);
        var thin = BuildInput(listings: 0);

        service.Score(liquid)!.Confidence.Value
            .Should().BeGreaterThan(service.Score(thin)!.Confidence.Value);
    }

    [Fact]
    public void Score_high_volatility_lowers_confidence_vs_stable_history()
    {
        var service = CreateService();

        var stable = BuildInput(history: PriceSeriesAround(10_000, 0.01, 12));
        var volatile_ = BuildInput(history: PriceSeriesAround(10_000, 0.40, 12));

        service.Score(stable)!.Confidence.Value
            .Should().BeGreaterThan(service.Score(volatile_)!.Confidence.Value);
    }

    [Fact]
    public void Score_urgent_expiry_raises_confidence()
    {
        var service = CreateService();

        var urgent = BuildInput(nearestSbcExpiry: NowUtc.AddHours(10));
        var distant = BuildInput(nearestSbcExpiry: NowUtc.AddDays(30));

        service.Score(urgent)!.Confidence.Value
            .Should().BeGreaterThan(service.Score(distant)!.Confidence.Value);
    }

    [Fact]
    public void Score_demand_bands_outside_overall_do_not_contribute()
    {
        var service = CreateService();

        // Band 87-89 não cobre overall 84 → demanda = 0
        var outOfRange = BuildInput(
            demandByBand: new[] { BandScore(87, 89, 1.0) },
            overallRating: 84);

        var match = BuildInput(
            demandByBand: new[] { BandScore(83, 85, 1.0) },
            overallRating: 84);

        service.Score(match)!.Confidence.Value
            .Should().BeGreaterThan(service.Score(outOfRange)!.Confidence.Value);
    }

    [Fact]
    public void Score_picks_max_demand_across_overlapping_bands()
    {
        var service = CreateService();

        var input = BuildInput(
            demandByBand: new[]
            {
                BandScore(82, 84, 0.30),
                BandScore(83, 85, 0.85),
                BandScore(84, 86, 0.50),
            },
            overallRating: 84);

        var result = service.Score(input);

        result.Should().NotBeNull();
        var demand = result!.Reasons.Single(r => r.Code == "DEMAND_OVERALL");
        // fator de demanda igual ao maior band score que cobre overall 84 (0.85)
        demand.Weight.Should().BeApproximately(0.85m, 0.01m);
    }

    [Fact]
    public void Score_with_custom_weights_changes_confidence()
    {
        var service = CreateService();
        var input = BuildInput(
            demandByBand: new[] { BandScore(84, 86, 1.0) },
            overallRating: 84,
            listings: 25,
            history: PriceSeriesAround(10_000, 0.01, 10),
            nearestSbcExpiry: NowUtc.AddHours(5));

        var defaultScore = service.Score(input)!.Confidence.Value;

        var spreadHeavy = new TradeScoringWeights(
            demand: 0.05, spread: 0.80, liquidity: 0.05, stability: 0.05, urgency: 0.05);

        var spreadHeavyScore = service.Score(input, spreadHeavy)!.Confidence.Value;

        spreadHeavyScore.Should().NotBe(defaultScore);
    }

    [Fact]
    public void TradeScoringWeights_rejects_non_normalized_weights()
    {
        var act = () => new TradeScoringWeights(
            demand: 0.5, spread: 0.5, liquidity: 0.5, stability: 0.5, urgency: 0.5);

        act.Should().Throw<ArgumentException>().WithMessage("*somar 1*");
    }

    [Fact]
    public void Score_throws_when_input_is_null()
    {
        var service = CreateService();
        var act = () => service.Score(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TradeScoringInput_rejects_non_utc_now()
    {
        var act = () => BuildInputWithNow(new DateTime(2026, 4, 22, 12, 0, 0, DateTimeKind.Local));
        act.Should().Throw<ArgumentException>().WithMessage("*UTC*");
    }

    [Fact]
    public void TradeScoringInput_rejects_currentPrice_with_other_player()
    {
        var other = new PlayerReference(999, "Other Player");
        var price = new PlayerPriceSnapshot(
            other, "futbin", NowUtc, new Coins(1000), null, new Coins(1200));

        var act = () => new TradeScoringInput(
            Player, 84, price,
            Array.Empty<PlayerPriceSnapshot>(),
            Array.Empty<MarketListingSnapshot>(),
            Array.Empty<RatingBandDemandScore>(),
            NowUtc);

        act.Should().Throw<ArgumentException>().WithMessage("*currentPrice.Player*");
    }

    [Fact]
    public void Score_opportunity_ExpectedNetMargin_matches_explicit_formula()
    {
        var service = CreateService();

        // buy=8000, sell=10000 → floor(10000*0.95) = 9500 → margem = 1500
        var input = BuildInput(buyNow: 8000, sellNow: null, median: 10_000);

        var result = service.Score(input);

        result.Should().NotBeNull();
        result!.ExpectedNetMargin.Value.Should().Be(1500);
        // Suggestion prices são consistentes com buy/sell esperados
        result.Suggestions.Single(s => s.Action == ExecutionAction.Buy)
            .TargetPrice.Value.Should().Be(8000);
        result.Suggestions.Single(s => s.Action == ExecutionAction.ListForSale)
            .TargetPrice.Value.Should().Be(10_000);
    }

    [Fact]
    public void Score_handles_empty_history_with_neutral_stability()
    {
        var service = CreateService();
        var input = BuildInput(history: Array.Empty<PlayerPriceSnapshot>());

        var result = service.Score(input);

        result.Should().NotBeNull();
        var stability = result!.Reasons.Single(r => r.Code == "PRICE_STABILITY");
        stability.Weight.Should().Be(0.5m); // neutral
    }

    [Fact]
    public void Score_handles_no_nearest_expiry_with_neutral_urgency()
    {
        var service = CreateService();
        var input = BuildInput(nearestSbcExpiry: null);

        var result = service.Score(input);

        result.Should().NotBeNull();
        var urgency = result!.Reasons.Single(r => r.Code == "SBC_EXPIRY_WINDOW");
        urgency.Weight.Should().Be(0.5m);
    }

    [Fact]
    public void Score_handles_expired_sbc_window_with_zero_urgency()
    {
        var service = CreateService();
        var input = BuildInput(nearestSbcExpiry: NowUtc.AddHours(-1));

        var result = service.Score(input);

        result.Should().NotBeNull();
        var urgency = result!.Reasons.Single(r => r.Code == "SBC_EXPIRY_WINDOW");
        urgency.Weight.Should().Be(0m);
    }

    // ------------------------- helpers -------------------------

    private static ITradeScoringService CreateService() =>
        new TradeScoringService(NullLogger<TradeScoringService>.Instance);

    private static TradeScoringInput BuildInput(
        decimal buyNow = 10_000,
        decimal? sellNow = null,
        decimal median = 12_000,
        int overallRating = 84,
        IReadOnlyList<RatingBandDemandScore>? demandByBand = null,
        int listings = 10,
        IReadOnlyList<PlayerPriceSnapshot>? history = null,
        DateTime? nearestSbcExpiry = null)
    {
        var price = new PlayerPriceSnapshot(
            Player,
            source: "futbin",
            capturedAtUtc: NowUtc,
            buyNowPrice: new Coins(buyNow),
            sellNowPrice: sellNow is null ? null : new Coins(sellNow.Value),
            medianMarketPrice: new Coins(median));

        var listingsList = Enumerable.Range(0, listings)
            .Select(i => new MarketListingSnapshot(
                listingId: $"L-{i}",
                player: Player,
                source: "ea-market",
                capturedAtUtc: NowUtc.AddMinutes(-i),
                startingBid: new Coins(buyNow - 100),
                buyNowPrice: new Coins(buyNow),
                expiresAtUtc: NowUtc.AddMinutes(-i).AddHours(1)))
            .ToArray();

        return new TradeScoringInput(
            player: Player,
            overallRating: overallRating,
            currentPrice: price,
            priceHistory: history ?? PriceSeriesAround(buyNow, 0.02, 8),
            recentListings: listingsList,
            demandByBand: demandByBand ?? Array.Empty<RatingBandDemandScore>(),
            nowUtc: NowUtc,
            nearestSbcExpiryUtc: nearestSbcExpiry);
    }

    private static TradeScoringInput BuildInputWithNow(DateTime now)
    {
        var price = new PlayerPriceSnapshot(
            Player, "futbin", NowUtc,
            new Coins(1000), null, new Coins(1200));

        return new TradeScoringInput(
            Player, 84, price,
            Array.Empty<PlayerPriceSnapshot>(),
            Array.Empty<MarketListingSnapshot>(),
            Array.Empty<RatingBandDemandScore>(),
            now);
    }

    private static IReadOnlyList<PlayerPriceSnapshot> PriceSeriesAround(decimal center, double relStdDev, int points)
    {
        var rand = new Random(42);
        var list = new List<PlayerPriceSnapshot>(points);
        for (var i = 0; i < points; i++)
        {
            var jitter = (rand.NextDouble() - 0.5) * 2.0 * relStdDev * (double)center;
            var buy = Math.Max(1m, center + (decimal)jitter);
            var median = buy + 100m;
            list.Add(new PlayerPriceSnapshot(
                Player,
                "futbin",
                NowUtc.AddHours(-i - 1),
                buyNowPrice: new Coins(buy),
                sellNowPrice: null,
                medianMarketPrice: new Coins(median)));
        }

        return list;
    }

    private static RatingBandDemandScore BandScore(int from, int to, double score) =>
        new(
            band: new RatingBand(from, to),
            score: score,
            totalRequiredCards: 5,
            contributingChallengeIds: new[] { Guid.NewGuid() },
            reasons: new[] { new DemandReason("AGGREGATED_DEMAND", "synthetic", score) });
}
