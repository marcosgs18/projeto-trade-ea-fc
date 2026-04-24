using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TradingIntel.Domain.ValueObjects;
using TradingIntel.Infrastructure.FutGg;
using Xunit;

namespace TradingIntel.Tests.Infrastructure.FutGg;

public sealed class FutGgPlayerMapperTests
{
    private static readonly PlayerReference Mbappe = new(231747, "Kylian Mbappé");

    private static string LoadFixture(string name)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "tests", "fixtures", "futgg", name);

        return File.ReadAllText(path);
    }

    [Fact]
    public void MapPriceSnapshot_real_fixture_produces_snapshot_with_source_and_median()
    {
        var parser = new FutGgPlayerPricesParser(NullLogger.Instance);
        var payload = parser.Parse(LoadFixture("player-prices-mbappe-pc.json"))!;
        var capturedAt = new DateTime(2026, 04, 23, 5, 5, 0, DateTimeKind.Utc);

        var mapper = new FutGgPlayerMapper(NullLogger.Instance);
        var snapshot = mapper.MapPriceSnapshot(payload, Mbappe, "pc", capturedAt);

        snapshot.Should().NotBeNull();
        snapshot!.Source.Should().Be("futgg:pc");
        snapshot.Player.Should().Be(Mbappe);
        snapshot.CapturedAtUtc.Should().Be(capturedAt);
        snapshot.BuyNowPrice.Value.Should().Be(payload.CurrentPrice!.Price);
        snapshot.SellNowPrice.Should().BeNull();
        snapshot.MedianMarketPrice.Value.Should().Be(payload.Overview!.AverageBin);
    }

    [Fact]
    public void MapPriceSnapshot_returns_null_when_current_price_is_missing()
    {
        var payload = new FutGgPlayerPricesPayload(1, null, Array.Empty<FutGgLiveAuction>(), null);
        var mapper = new FutGgPlayerMapper(NullLogger.Instance);

        mapper.MapPriceSnapshot(payload, Mbappe, "pc", DateTime.UtcNow).Should().BeNull();
    }

    [Fact]
    public void MapPriceSnapshot_returns_null_when_card_is_untradeable()
    {
        var cp = new FutGgCurrentPrice(1000m, "pc", IsExtinct: false, IsUntradeable: true, PriceUpdatedAt: null);
        var payload = new FutGgPlayerPricesPayload(1, cp, Array.Empty<FutGgLiveAuction>(), null);

        var mapper = new FutGgPlayerMapper(NullLogger.Instance);

        mapper.MapPriceSnapshot(payload, Mbappe, "pc", DateTime.UtcNow).Should().BeNull();
    }

    [Fact]
    public void MapPriceSnapshot_falls_back_to_current_price_when_overview_missing()
    {
        var cp = new FutGgCurrentPrice(5000m, "pc", IsExtinct: false, IsUntradeable: false, PriceUpdatedAt: null);
        var payload = new FutGgPlayerPricesPayload(1, cp, Array.Empty<FutGgLiveAuction>(), null);
        var mapper = new FutGgPlayerMapper(NullLogger.Instance);

        var snapshot = mapper.MapPriceSnapshot(payload, Mbappe, "pc", DateTime.UtcNow);

        snapshot.Should().NotBeNull();
        snapshot!.MedianMarketPrice.Value.Should().Be(5000m);
    }

    [Fact]
    public void MapLiveListings_real_fixture_emits_only_future_auctions_and_respects_invariants()
    {
        var parser = new FutGgPlayerPricesParser(NullLogger.Instance);
        var payload = parser.Parse(LoadFixture("player-prices-mbappe-pc.json"))!;
        var capturedAt = new DateTime(2026, 04, 23, 5, 5, 0, DateTimeKind.Utc);

        var mapper = new FutGgPlayerMapper(NullLogger.Instance);
        var listings = mapper.MapLiveListings(payload, Mbappe, "pc", capturedAt);

        listings.Should().NotBeEmpty();
        listings.Should().OnlyContain(l => l.ExpiresAtUtc > capturedAt);
        listings.Should().OnlyContain(l => l.BuyNowPrice.Value >= l.StartingBid.Value);
        listings.Should().OnlyContain(l => l.Source == "futgg:pc");
        listings.Select(l => l.ListingId).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void MapLiveListings_skips_expired_auctions()
    {
        var auctions = new[]
        {
            new FutGgLiveAuction(1000m, 900m, DateTime.UtcNow.AddMinutes(-5)),
            new FutGgLiveAuction(1200m, 1100m, DateTime.UtcNow.AddMinutes(5)),
        };
        var payload = new FutGgPlayerPricesPayload(1, null, auctions, null);
        var mapper = new FutGgPlayerMapper(NullLogger.Instance);

        var listings = mapper.MapLiveListings(payload, Mbappe, "pc", DateTime.UtcNow);

        listings.Should().HaveCount(1);
        listings.Single().BuyNowPrice.Value.Should().Be(1200m);
    }

    [Fact]
    public void MapLiveListings_skips_auction_where_bin_is_below_starting_bid()
    {
        var auctions = new[]
        {
            new FutGgLiveAuction(900m, 1000m, DateTime.UtcNow.AddMinutes(5)),
        };
        var payload = new FutGgPlayerPricesPayload(1, null, auctions, null);
        var mapper = new FutGgPlayerMapper(NullLogger.Instance);

        mapper.MapLiveListings(payload, Mbappe, "pc", DateTime.UtcNow).Should().BeEmpty();
    }
}
