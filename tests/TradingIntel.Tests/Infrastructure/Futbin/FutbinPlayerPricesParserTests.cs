using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TradingIntel.Infrastructure.Futbin;
using Xunit;

namespace TradingIntel.Tests.Infrastructure.Futbin;

public sealed class FutbinPlayerPricesParserTests
{
    private static string LoadFixture(string name)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "tests", "fixtures", "futbin", name);

        return File.ReadAllText(path);
    }

    [Fact]
    public void ParsePlayerPrices_full_payload_returns_all_platforms()
    {
        var parser = new FutbinPlayerPricesParser(NullLogger.Instance);

        var parsed = parser.ParsePlayerPrices(LoadFixture("player-prices-full.json"));

        parsed.Should().NotBeNull();
        parsed!.PlayerId.Should().Be(1);
        parsed.Platforms.Select(p => p.Platform).Should().BeEquivalentTo(new[] { "ps", "xbox", "pc" });

        var ps = parsed.Platforms.Single(p => p.Platform == "ps");
        ps.LowestBinPrice.Should().Be(1_500_000);
        ps.SecondLowestBinPrice.Should().Be(1_510_000);
        ps.MinPriceRange.Should().Be(900_000);
        ps.MaxPriceRange.Should().Be(2_000_000);
        ps.RecentPercent.Should().Be(85);
        ps.Updated.Should().Be("3 minutes ago");
    }

    [Fact]
    public void ParsePlayerPrices_partial_payload_skips_zero_prices_and_missing_platforms()
    {
        var parser = new FutbinPlayerPricesParser(NullLogger.Instance);

        var parsed = parser.ParsePlayerPrices(LoadFixture("player-prices-partial.json"));

        parsed.Should().NotBeNull();
        parsed!.PlayerId.Should().Be(42);
        parsed.Platforms.Select(p => p.Platform).Should().BeEquivalentTo(new[] { "ps", "pc" });

        var ps = parsed.Platforms.Single(p => p.Platform == "ps");
        ps.LowestBinPrice.Should().BeNull();

        var pc = parsed.Platforms.Single(p => p.Platform == "pc");
        pc.LowestBinPrice.Should().Be(12_500);
        pc.RecentPercent.Should().BeNull();
    }

    [Fact]
    public void ParsePlayerPrices_empty_payload_returns_null()
    {
        var parser = new FutbinPlayerPricesParser(NullLogger.Instance);

        parser.ParsePlayerPrices(LoadFixture("player-prices-empty.json")).Should().BeNull();
    }

    [Fact]
    public void ParsePlayerPrices_malformed_payload_returns_null()
    {
        var parser = new FutbinPlayerPricesParser(NullLogger.Instance);

        parser.ParsePlayerPrices(LoadFixture("player-prices-malformed.json")).Should().BeNull();
    }

    [Fact]
    public void ParsePlayerPrices_whitespace_payload_returns_null()
    {
        var parser = new FutbinPlayerPricesParser(NullLogger.Instance);

        parser.ParsePlayerPrices("   ").Should().BeNull();
    }

    [Fact]
    public void ParsePlayerPrices_invalid_json_returns_null()
    {
        var parser = new FutbinPlayerPricesParser(NullLogger.Instance);

        parser.ParsePlayerPrices("{oops:").Should().BeNull();
    }
}
