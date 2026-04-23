using FluentAssertions;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;
using Xunit;

namespace TradingIntel.Tests.Domain;

public sealed class DomainInvariantsTests
{
    [Fact]
    public void SbcChallenge_requires_at_least_one_requirement()
    {
        var act = () => new SbcChallenge(
            Guid.NewGuid(),
            "84+ Upgrade",
            "upgrades",
            expiresAtUtc: null,
            repeatability: SbcRepeatability.Unknown(),
            setName: "Daily",
            observedAtUtc: DateTime.UtcNow,
            Array.Empty<SbcRequirement>());

        act.Should().Throw<ArgumentException>().WithMessage("*At least one SBC requirement*");
    }

    [Fact]
    public void SbcRequirement_rejects_maximum_less_than_minimum()
    {
        var act = () => new SbcRequirement("minimum_chemistry", 30, 29);
        act.Should().Throw<ArgumentException>().WithMessage("*maximum*");
    }

    [Fact]
    public void RatingBand_validates_range_bounds()
    {
        var act = () => new RatingBand(90, 80);
        act.Should().Throw<ArgumentException>().WithMessage("*greater than or equal*");
    }

    [Fact]
    public void PlayerPriceSnapshot_rejects_sell_price_below_buy_price()
    {
        var player = new PlayerReference(12345, "Sample Player");

        var act = () => new PlayerPriceSnapshot(
            player,
            "futbin",
            DateTime.UtcNow,
            new Coins(2000),
            new Coins(1999),
            new Coins(2050));

        act.Should().Throw<ArgumentException>().WithMessage("*sellNowPrice*");
    }

    [Fact]
    public void MarketListingSnapshot_rejects_expired_listing()
    {
        var now = DateTime.UtcNow;
        var player = new PlayerReference(12, "Another Player");

        var act = () => new MarketListingSnapshot(
            "listing-1",
            player,
            "ea-market",
            now,
            new Coins(1500),
            new Coins(1800),
            now);

        act.Should().Throw<ArgumentException>().WithMessage("*expiresAtUtc*");
    }

    [Fact]
    public void TradeOpportunity_computes_profit_from_prices()
    {
        var opportunity = CreateValidOpportunity();

        opportunity.ExpectedProfit.Value.Should().Be(500);
    }

    [Fact]
    public void TradeOpportunity_computes_net_margin_after_EA_tax()
    {
        // buy=1000, sell=1500 → floor(1500 * 0.95) - 1000 = 1425 - 1000 = 425
        var opportunity = CreateValidOpportunity();

        opportunity.ExpectedNetMargin.Value.Should().Be(425);
    }

    [Fact]
    public void TradeOpportunity_net_margin_clamps_to_zero_when_tax_erases_edge()
    {
        var player = new PlayerReference(123, "Thin Edge");
        var reason = new OpportunityReason("THIN", "Edge abaixo da taxa", 0.1m);
        var suggestion = new ExecutionSuggestion(
            Guid.NewGuid(), Guid.NewGuid(), ExecutionAction.Buy,
            new Coins(1000), DateTime.UtcNow.AddMinutes(5));

        // sell=1010, buy=1000 → gross 10 lucro; net = floor(1010*0.95)=959 - 1000 = -41 → clamp 0
        var opp = new TradeOpportunity(
            Guid.NewGuid(),
            player,
            DateTime.UtcNow,
            new Coins(1000),
            new Coins(1010),
            new ConfidenceScore(0.3m),
            new[] { reason },
            new[] { suggestion });

        opp.ExpectedNetMargin.Value.Should().Be(0);
    }

    [Fact]
    public void TradeOpportunity_requires_reasons()
    {
        var player = new PlayerReference(99, "No Reason Player");
        var suggestion = new ExecutionSuggestion(Guid.NewGuid(), Guid.NewGuid(), ExecutionAction.Buy, new Coins(1200), DateTime.UtcNow.AddMinutes(5));

        var act = () => new TradeOpportunity(
            Guid.NewGuid(),
            player,
            DateTime.UtcNow,
            new Coins(1000),
            new Coins(1500),
            new ConfidenceScore(0.82m),
            Array.Empty<OpportunityReason>(),
            new[] { suggestion });

        act.Should().Throw<ArgumentException>().WithMessage("*At least one reason*");
    }

    [Fact]
    public void PortfolioPosition_rejects_non_positive_quantity()
    {
        var player = new PlayerReference(77, "Invalid Quantity");

        var act = () => new PortfolioPosition(player, 0, new Coins(800), DateTime.UtcNow);

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*greater than zero*");
    }

    [Fact]
    public void SourceSnapshotMetadata_requires_positive_record_count()
    {
        var act = () => new SourceSnapshotMetadata(
            "futwiz",
            DateTime.UtcNow,
            0,
            Guid.NewGuid().ToString("N"),
            "ABC123");

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*greater than zero*");
    }

    [Fact]
    public void SourceSnapshotMetadata_generates_sha256_hash()
    {
        var hash = SourceSnapshotMetadata.ComputePayloadHash("{\"test\":true}");

        hash.Should().NotBeNullOrWhiteSpace();
        hash.Length.Should().Be(64);
    }

    private static TradeOpportunity CreateValidOpportunity()
    {
        var opportunityId = Guid.NewGuid();
        var reason = new OpportunityReason("UNDER_MARKET", "Listing below expected median.", 0.7m);
        var suggestion = new ExecutionSuggestion(Guid.NewGuid(), opportunityId, ExecutionAction.Buy, new Coins(1000), DateTime.UtcNow.AddMinutes(10));

        return new TradeOpportunity(
            opportunityId,
            new PlayerReference(123, "Profitable Player"),
            DateTime.UtcNow,
            new Coins(1000),
            new Coins(1500),
            new ConfidenceScore(0.9m),
            new[] { reason },
            new[] { suggestion });
    }
}