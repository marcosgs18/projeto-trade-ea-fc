using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TradingIntel.Application.Sbc;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;
using Xunit;

namespace TradingIntel.Tests.Application.Sbc;

public sealed class RatingBandDemandServiceTests
{
    private static readonly DateTime NowUtc = new(2026, 04, 22, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ComputeDemand_returns_empty_when_no_challenges()
    {
        var service = CreateService();

        var result = service.ComputeDemand(Array.Empty<SbcChallenge>(), NowUtc);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeDemand_throws_when_nowUtc_is_not_utc()
    {
        var service = CreateService();
        var localNow = new DateTime(2026, 04, 22, 12, 0, 0, DateTimeKind.Local);

        var act = () => service.ComputeDemand(Array.Empty<SbcChallenge>(), localNow);

        act.Should().Throw<ArgumentException>().WithMessage("*UTC*");
    }

    [Fact]
    public void ComputeDemand_skips_expired_challenges()
    {
        var service = CreateService();
        var expired = BuildChallenge(
            title: "Old SBC",
            category: "upgrades",
            repeatability: SbcRepeatability.Unlimited(),
            expiresAtUtc: NowUtc.AddHours(-1),
            requirements: new[] { new SbcRequirement("min_team_rating_84", 84) });

        var result = service.ComputeDemand(new[] { expired }, NowUtc);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeDemand_ignores_requirements_without_rating_signal()
    {
        var service = CreateService();
        var challenge = BuildChallenge(
            title: "Chemistry only",
            category: "upgrades",
            repeatability: SbcRepeatability.Unlimited(),
            expiresAtUtc: NowUtc.AddDays(5),
            requirements: new[]
            {
                new SbcRequirement("min_chemistry_25", 25),
                new SbcRequirement("exactly_11_players_rare", 11),
            });

        var result = service.ComputeDemand(new[] { challenge }, NowUtc);

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("min_team_rating_83", 83, 83, 85)]
    [InlineData("min_squad_rating_85", 85, 85, 87)]
    [InlineData("exactly_3_players_84_overall", 3, 84, 86)]
    [InlineData("min_rating_88", 88, 88, 90)]
    public void ComputeDemand_interprets_rating_requirement_variants(string key, int requirementMinimum, int expectedBandFrom, int expectedBandTo)
    {
        var service = CreateService();
        var challenge = BuildChallenge(
            title: "Rating Probe",
            category: "upgrades",
            repeatability: SbcRepeatability.Unlimited(),
            expiresAtUtc: NowUtc.AddDays(3),
            requirements: new[] { new SbcRequirement(key, requirementMinimum) });

        var result = service.ComputeDemand(new[] { challenge }, NowUtc);

        result.Should().HaveCount(1);
        result[0].Band.FromRating.Should().Be(expectedBandFrom);
        result[0].Band.ToRating.Should().Be(expectedBandTo);
    }

    [Fact]
    public void ComputeDemand_repeatable_outscores_non_repeatable_equivalent()
    {
        var service = CreateService();

        var unlimited = BuildChallenge(
            title: "Daily 84 Upgrade",
            category: "upgrades",
            repeatability: SbcRepeatability.Unlimited(),
            expiresAtUtc: NowUtc.AddDays(3),
            requirements: new[] { new SbcRequirement("min_team_rating_84", 84) });

        var oneShot = BuildChallenge(
            title: "One-off 84 Icon Swap",
            category: "upgrades",
            repeatability: SbcRepeatability.NotRepeatable(),
            expiresAtUtc: NowUtc.AddDays(3),
            requirements: new[] { new SbcRequirement("min_team_rating_84", 84) });

        var unlimitedScore = service.ComputeDemand(new[] { unlimited }, NowUtc).Single().Score;
        var oneShotScore = service.ComputeDemand(new[] { oneShot }, NowUtc).Single().Score;

        unlimitedScore.Should().BeGreaterThan(oneShotScore);
    }

    [Fact]
    public void ComputeDemand_urgent_challenges_outscore_distant_ones()
    {
        var service = CreateService();

        var urgent = BuildChallenge(
            title: "Urgent 85",
            category: "players",
            repeatability: SbcRepeatability.Limited(2),
            expiresAtUtc: NowUtc.AddHours(10),
            requirements: new[] { new SbcRequirement("min_team_rating_85", 85) });

        var distant = BuildChallenge(
            title: "Distant 85",
            category: "players",
            repeatability: SbcRepeatability.Limited(2),
            expiresAtUtc: NowUtc.AddDays(20),
            requirements: new[] { new SbcRequirement("min_team_rating_85", 85) });

        var urgentScore = service.ComputeDemand(new[] { urgent }, NowUtc).Single().Score;
        var distantScore = service.ComputeDemand(new[] { distant }, NowUtc).Single().Score;

        urgentScore.Should().BeGreaterThan(distantScore);
    }

    [Fact]
    public void ComputeDemand_aggregates_same_band_across_challenges()
    {
        var service = CreateService();

        var a = BuildChallenge(
            title: "84 Team Rating",
            category: "upgrades",
            repeatability: SbcRepeatability.Unlimited(),
            expiresAtUtc: NowUtc.AddDays(3),
            requirements: new[] { new SbcRequirement("min_team_rating_84", 84) });

        var b = BuildChallenge(
            title: "Exactly 3x 85+",
            category: "upgrades",
            repeatability: SbcRepeatability.Limited(3),
            expiresAtUtc: NowUtc.AddDays(2),
            requirements: new[] { new SbcRequirement("exactly_3_players_85_overall", 3) });

        var c = BuildChallenge(
            title: "Player Swap 84",
            category: "players",
            repeatability: SbcRepeatability.NotRepeatable(),
            expiresAtUtc: NowUtc.AddDays(1),
            requirements: new[] { new SbcRequirement("min_team_rating_84", 84) });

        var result = service.ComputeDemand(new[] { a, b, c }, NowUtc);

        result.Should().NotBeEmpty();
        var band84 = result.Single(r => r.Band.FromRating == 84);
        band84.ContributingChallengeIds.Should().Contain(new[] { a.Id, c.Id });
        band84.Reasons.Should().Contain(reason => reason.Code == "AGGREGATED_DEMAND");
        band84.Reasons.Should().Contain(reason => reason.Code == "CHALLENGE_REPEATABLE_UNLIMITED");
        band84.Reasons.Should().Contain(reason => reason.Code == "CHALLENGE_ONE_SHOT");
        band84.TotalRequiredCards.Should().Be(2); // team_rating defaults to 1 card per challenge
    }

    [Fact]
    public void ComputeDemand_uses_player_count_when_requirement_declares_it()
    {
        var service = CreateService();
        var challenge = BuildChallenge(
            title: "Exactly 5x 87+",
            category: "upgrades",
            repeatability: SbcRepeatability.Unlimited(),
            expiresAtUtc: NowUtc.AddDays(2),
            requirements: new[] { new SbcRequirement("exactly_5_players_87_overall", 5) });

        var result = service.ComputeDemand(new[] { challenge }, NowUtc).Single();

        result.Band.FromRating.Should().Be(87);
        result.TotalRequiredCards.Should().Be(5);
        result.Reasons.Should().Contain(r => r.Message.Contains("5 carta(s) 87+ overall"));
    }

    [Fact]
    public void ComputeDemand_orders_by_score_descending_then_band()
    {
        var service = CreateService();

        var low = BuildChallenge(
            title: "Distant 80",
            category: "foundations",
            repeatability: SbcRepeatability.NotRepeatable(),
            expiresAtUtc: NowUtc.AddDays(20),
            requirements: new[] { new SbcRequirement("min_team_rating_80", 80) });

        var high = BuildChallenge(
            title: "Urgent 86 Daily",
            category: "upgrades",
            repeatability: SbcRepeatability.Unlimited(),
            expiresAtUtc: NowUtc.AddHours(6),
            requirements: new[] { new SbcRequirement("exactly_4_players_86_overall", 4) });

        var result = service.ComputeDemand(new[] { low, high }, NowUtc);

        result.Should().HaveCountGreaterThan(1);
        result.First().Band.FromRating.Should().Be(86);
    }

    [Fact]
    public void ComputeDemand_reasons_are_readable_and_bounded_in_01()
    {
        var service = CreateService();
        var challenge = BuildChallenge(
            title: "Weekend 84",
            category: "players",
            repeatability: SbcRepeatability.Unlimited(),
            expiresAtUtc: NowUtc.AddHours(30),
            requirements: new[] { new SbcRequirement("min_team_rating_84", 84) });

        var score = service.ComputeDemand(new[] { challenge }, NowUtc).Single();

        score.Score.Should().BeInRange(0, 1);
        score.Reasons.Should().NotBeEmpty();
        score.Reasons.Should().OnlyContain(r => r.Weight >= 0 && r.Weight <= 1);
        score.Reasons.Should().Contain(r => r.Message.Contains("Weekend 84"));
    }

    [Fact]
    public void ComputeDemand_respects_custom_weights()
    {
        var service = CreateService();
        var challenge = BuildChallenge(
            title: "Volume focused",
            category: "upgrades",
            repeatability: SbcRepeatability.Unlimited(),
            expiresAtUtc: NowUtc.AddDays(5),
            requirements: new[] { new SbcRequirement("exactly_11_players_84_overall", 11) });

        var defaultScore = service.ComputeDemand(new[] { challenge }, NowUtc).Single().Score;

        var volumeHeavy = new RatingBandDemandWeights(
            cardVolume: 0.9, repeatability: 0.05, expiryUrgency: 0.025, category: 0.025);
        var volumeScore = service.ComputeDemand(new[] { challenge }, NowUtc, volumeHeavy).Single().Score;

        volumeScore.Should().NotBe(defaultScore);
    }

    [Fact]
    public void RatingBandDemandWeights_rejects_non_normalized_weights()
    {
        var act = () => new RatingBandDemandWeights(0.5, 0.5, 0.5, 0.5);

        act.Should().Throw<ArgumentException>().WithMessage("*somar 1*");
    }

    [Fact]
    public void ComputeDemand_limits_reasons_per_band_to_configured_cap()
    {
        var service = CreateService();
        var weights = new RatingBandDemandWeights(maxReasonsPerBand: 2);

        var challenges = Enumerable.Range(0, 5)
            .Select(i => BuildChallenge(
                title: $"SBC {i} 84",
                category: "upgrades",
                repeatability: SbcRepeatability.Unlimited(),
                expiresAtUtc: NowUtc.AddDays(2),
                requirements: new[] { new SbcRequirement("min_team_rating_84", 84) }))
            .ToArray();

        var score = service.ComputeDemand(challenges, NowUtc, weights).Single(r => r.Band.FromRating == 84);

        // AGGREGATED_DEMAND + at most `maxReasonsPerBand` challenge reasons.
        score.Reasons.Should().HaveCount(1 + 2);
    }

    private static IRatingBandDemandService CreateService() =>
        new RatingBandDemandService(NullLogger<RatingBandDemandService>.Instance);

    private static SbcChallenge BuildChallenge(
        string title,
        string category,
        SbcRepeatability repeatability,
        DateTime? expiresAtUtc,
        IReadOnlyList<SbcRequirement> requirements)
    {
        return new SbcChallenge(
            id: Guid.NewGuid(),
            title: title,
            category: category,
            expiresAtUtc: expiresAtUtc,
            repeatability: repeatability,
            setName: "test-set",
            observedAtUtc: NowUtc,
            requirements: requirements);
    }
}
