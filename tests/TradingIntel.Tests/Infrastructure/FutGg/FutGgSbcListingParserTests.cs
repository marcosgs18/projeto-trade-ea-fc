using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TradingIntel.Infrastructure.FutGg;
using Xunit;

namespace TradingIntel.Tests.Infrastructure.FutGg;

public sealed class FutGgSbcListingParserTests
{
    [Fact]
    public void ParseListing_extracts_items_from_real_fixture()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tests", "fixtures", "futgg", "sbc-listing-rendered.txt");
        var payload = File.ReadAllText(fixturePath);

        var parser = new FutGgSbcListingParser(NullLogger.Instance);
        var capturedAt = new DateTime(2026, 04, 22, 12, 0, 0, DateTimeKind.Utc);

        var items = parser.ParseListing(payload, capturedAt);

        items.Should().NotBeEmpty();
        items.Should().Contain(i => i.Category == "upgrades" || i.Category == "players" || i.Category == "challenges");

        var expiring = items.FirstOrDefault(i => i.ExpiresAtUtc is not null);
        expiring.Should().NotBeNull();
        expiring!.ExpiresAtUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void ParseListing_is_resilient_to_missing_metadata_lines()
    {
        var payload = """
Title: test

### [Sample SBC 1,000![Image 1: FC Coin](x)](https://www.fut.gg/sbc/upgrades/26-999-sample/)
Some random line without expires/repeatable.
""";

        var parser = new FutGgSbcListingParser(NullLogger.Instance);
        var items = parser.ParseListing(payload, DateTime.UtcNow);

        items.Should().HaveCount(1);
        items[0].ExpiresAtUtc.Should().BeNull();
        items[0].RepeatableCount.Should().BeNull();
        items[0].RepeatableUnlimited.Should().BeFalse();
    }
}

