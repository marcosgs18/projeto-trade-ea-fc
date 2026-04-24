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

    [Fact]
    public void ParseListing_handles_current_header_shapes()
    {
        // Regression fixture built from the headers FUT.GG currently renders via
        // r.jina.ai (mixture of: title+coin icon, title only, and legacy
        // title+coin-amount+icon). All three must produce valid items with the
        // title stripped of any coin amount.
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "tests", "fixtures", "futgg", "sbc-listing-headers.md");
        var payload = File.ReadAllText(fixturePath);

        var parser = new FutGgSbcListingParser(NullLogger.Instance);
        var capturedAt = new DateTime(2026, 04, 23, 04, 28, 56, DateTimeKind.Utc);

        var items = parser.ParseListing(payload, capturedAt);

        items.Should().HaveCount(6);

        items.Should().Contain(i => i.Title == "Ederson" && i.Category == "players");
        items.Should().Contain(i => i.Title == "2x 86+ Upgrade" && i.Category == "upgrades" && i.RepeatableCount == 3);
        items.Should().Contain(i => i.Title == "TOTS Challenge 2" && i.Category == "challenges" && i.RepeatableUnlimited);
        items.Should().Contain(i => i.Title == "Marquee Matchups" && i.Category == "challenges");
        items.Should().Contain(i => i.Title == "Premier League POTM March" && i.Category == "players");

        // Legacy shape with embedded coin amount must still produce a clean title.
        items.Should().Contain(i =>
            i.Title == "Ederson" &&
            i.DetailsUrl.EndsWith("26-830-ederson-legacy/", StringComparison.Ordinal));

        items.Should().AllSatisfy(i =>
        {
            i.Title.Should().NotMatch(@".*\d{1,3}(,\d{3})+.*");
            i.DetailsUrl.Should().StartWith("https://www.fut.gg/sbc/");
        });
    }
}

