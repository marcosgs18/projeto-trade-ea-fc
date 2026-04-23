using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TradingIntel.Infrastructure.FutGg;
using Xunit;

namespace TradingIntel.Tests.Infrastructure.FutGg;

public sealed class FutGgSbcDetailParserTests
{
    [Fact]
    public void ParseVisibleRequirements_extracts_bullet_requirements_from_real_fixture()
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "tests", "fixtures", "futgg", "sbc-detail-ederson-rendered.txt");

        var payload = File.ReadAllText(fixturePath);
        var parser = new FutGgSbcDetailParser(NullLogger.Instance);

        var requirements = parser.ParseVisibleRequirements(payload);

        requirements.Should().NotBeEmpty();
        requirements.Should().Contain(r => r.Contains("Min. Team Rating: 88"));
        requirements.Should().Contain(r => r.Contains("Min. 1 Players from: Brazil"));
    }

    [Fact]
    public void ParseVisibleRequirements_returns_empty_for_blank_payload()
    {
        var parser = new FutGgSbcDetailParser(NullLogger.Instance);

        var requirements = parser.ParseVisibleRequirements("   ");

        requirements.Should().BeEmpty();
    }
}

