using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TradingIntel.Infrastructure.FutGg;
using Xunit;

namespace TradingIntel.Tests.Infrastructure.FutGg;

public sealed class FutGgSbcMapperTests
{
    [Fact]
    public void Map_creates_valid_SbcChallenge()
    {
        var mapper = new FutGgSbcMapper(NullLogger.Instance);

        var parsed = new FutGgSbcListingItemParsed(
            Title: "2x 86+ Upgrade",
            Category: "upgrades",
            DetailsUrl: "https://www.fut.gg/sbc/upgrades/26-784-2x-86-upgrade/",
            ExpiresAtUtc: new DateTime(2026, 04, 29, 0, 0, 0, DateTimeKind.Utc),
            RepeatableCount: 3,
            RepeatableUnlimited: false,
            RequirementLines: new[] { "Min. Team Rating: 83", "Exactly 11 Players: Rare" });

        var challenge = mapper.Map(parsed, new DateTime(2026, 04, 22, 10, 0, 0, DateTimeKind.Utc));

        challenge.Title.Should().Be("2x 86+ Upgrade");
        challenge.Category.Should().Be("upgrades");
        challenge.ExpiresAtUtc.Should().NotBeNull();
        challenge.Requirements.Should().HaveCount(2);
        challenge.Requirements.Should().Contain(r => r.Key.Contains("team_rating") && r.Minimum == 83);
    }
}

