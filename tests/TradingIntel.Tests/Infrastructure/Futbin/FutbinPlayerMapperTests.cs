using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TradingIntel.Domain.ValueObjects;
using TradingIntel.Infrastructure.Futbin;
using Xunit;

namespace TradingIntel.Tests.Infrastructure.Futbin;

public sealed class FutbinPlayerMapperTests
{
    private static FutbinPlayerPricesParsed ParseFixture(string name)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "tests", "fixtures", "futbin", name);

        var payload = File.ReadAllText(path);
        var parser = new FutbinPlayerPricesParser(NullLogger.Instance);
        var parsed = parser.ParsePlayerPrices(payload);
        parsed.Should().NotBeNull();
        return parsed!;
    }

    [Fact]
    public void MapPriceSnapshots_full_payload_maps_all_platforms_with_metadata()
    {
        var parsed = ParseFixture("player-prices-full.json");
        var mapper = new FutbinPlayerMapper(NullLogger.Instance);
        var player = new PlayerReference(1, "Lionel Messi");
        var capturedAt = new DateTime(2026, 04, 22, 10, 0, 0, DateTimeKind.Utc);

        var snapshots = mapper.MapPriceSnapshots(parsed, player, capturedAt);

        snapshots.Should().HaveCount(3);
        snapshots.Should().AllSatisfy(s =>
        {
            s.Player.PlayerId.Should().Be(1);
            s.Source.Should().StartWith("futbin:");
            s.CapturedAtUtc.Should().Be(capturedAt);
            s.BuyNowPrice.Value.Should().BeGreaterThan(0);
        });

        snapshots.Should().Contain(s => s.Source == "futbin:ps" && s.BuyNowPrice.Value == 1_500_000);
    }

    [Fact]
    public void MapPriceSnapshots_partial_payload_maps_only_platforms_with_price()
    {
        var parsed = ParseFixture("player-prices-partial.json");
        var mapper = new FutbinPlayerMapper(NullLogger.Instance);
        var player = new PlayerReference(42, "Jogador Teste");
        var capturedAt = new DateTime(2026, 04, 22, 10, 0, 0, DateTimeKind.Utc);

        var snapshots = mapper.MapPriceSnapshots(parsed, player, capturedAt);

        snapshots.Should().HaveCount(1);
        snapshots[0].Source.Should().Be("futbin:pc");
        snapshots[0].BuyNowPrice.Value.Should().Be(12_500);
    }

    [Fact]
    public void MapLowestListingSnapshots_emits_one_listing_per_platform_with_price()
    {
        var parsed = ParseFixture("player-prices-full.json");
        var mapper = new FutbinPlayerMapper(NullLogger.Instance);
        var player = new PlayerReference(1, "Lionel Messi");
        var capturedAt = new DateTime(2026, 04, 22, 10, 0, 0, DateTimeKind.Utc);

        var listings = mapper.MapLowestListingSnapshots(parsed, player, capturedAt);

        listings.Should().HaveCount(3);
        listings.Should().AllSatisfy(listing =>
        {
            listing.CapturedAtUtc.Should().Be(capturedAt);
            listing.ExpiresAtUtc.Should().BeAfter(capturedAt);
            listing.StartingBid.Value.Should().Be(listing.BuyNowPrice.Value);
            listing.ListingId.Should().NotBeNullOrWhiteSpace();
            listing.Source.Should().StartWith("futbin:");
        });
    }

    [Fact]
    public void MapPriceSnapshots_returns_empty_when_all_platforms_are_missing_price()
    {
        var parsed = new FutbinPlayerPricesParsed(
            PlayerId: 7,
            Platforms: new[]
            {
                new FutbinPlatformPriceParsed("ps", null, null, null, null, null, null),
                new FutbinPlatformPriceParsed("pc", null, null, null, null, null, null)
            });

        var mapper = new FutbinPlayerMapper(NullLogger.Instance);
        var player = new PlayerReference(7, "Sem Preço");
        var capturedAt = new DateTime(2026, 04, 22, 10, 0, 0, DateTimeKind.Utc);

        mapper.MapPriceSnapshots(parsed, player, capturedAt).Should().BeEmpty();
        mapper.MapLowestListingSnapshots(parsed, player, capturedAt).Should().BeEmpty();
    }
}
