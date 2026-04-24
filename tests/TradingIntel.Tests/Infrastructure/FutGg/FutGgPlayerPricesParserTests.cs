using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TradingIntel.Infrastructure.FutGg;
using Xunit;

namespace TradingIntel.Tests.Infrastructure.FutGg;

public sealed class FutGgPlayerPricesParserTests
{
    private static string LoadFixture(string name)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "tests", "fixtures", "futgg", name);

        return File.ReadAllText(path);
    }

    [Fact]
    public void Parse_real_player_prices_payload_returns_all_sections()
    {
        var parser = new FutGgPlayerPricesParser(NullLogger.Instance);

        var parsed = parser.Parse(LoadFixture("player-prices-mbappe-pc.json"));

        parsed.Should().NotBeNull();
        parsed!.EaId.Should().Be(231747);

        parsed.CurrentPrice.Should().NotBeNull();
        parsed.CurrentPrice!.Platform.Should().Be("pc");
        parsed.CurrentPrice.Price.Should().BeGreaterThan(0);
        parsed.CurrentPrice.IsExtinct.Should().BeFalse();
        parsed.CurrentPrice.IsUntradeable.Should().BeFalse();
        parsed.CurrentPrice.PriceUpdatedAt.Should().NotBeNull();
        parsed.CurrentPrice.PriceUpdatedAt!.Value.Kind.Should().Be(DateTimeKind.Utc);

        parsed.Overview.Should().NotBeNull();
        parsed.Overview!.AverageBin.Should().BeGreaterThan(0);

        parsed.LiveAuctions.Should().NotBeEmpty();
        parsed.LiveAuctions.Should().OnlyContain(a => a.EndDateUtc.Kind == DateTimeKind.Utc);
        parsed.LiveAuctions.Should().OnlyContain(a => a.BuyNowPrice > 0m);
    }

    [Fact]
    public void Parse_returns_null_when_payload_is_empty()
    {
        var parser = new FutGgPlayerPricesParser(NullLogger.Instance);

        parser.Parse(string.Empty).Should().BeNull();
        parser.Parse("   ").Should().BeNull();
    }

    [Fact]
    public void Parse_returns_null_when_data_section_is_missing()
    {
        var parser = new FutGgPlayerPricesParser(NullLogger.Instance);

        parser.Parse("{\"meta\":{}}").Should().BeNull();
    }

    [Fact]
    public void Parse_tolerates_missing_overview_and_live_auctions()
    {
        var parser = new FutGgPlayerPricesParser(NullLogger.Instance);

        var payload = "{\"data\":{\"eaId\":99,\"currentPrice\":{\"eaId\":99,\"platform\":\"pc\",\"price\":1000,\"isExtinct\":false,\"isSbc\":false,\"isObjective\":false,\"isUntradeable\":false,\"priceUpdatedAt\":\"2026-04-23T00:00:00Z\"}}}";

        var parsed = parser.Parse(payload);

        parsed.Should().NotBeNull();
        parsed!.EaId.Should().Be(99);
        parsed.CurrentPrice!.Price.Should().Be(1000m);
        parsed.Overview.Should().BeNull();
        parsed.LiveAuctions.Should().BeEmpty();
    }
}
