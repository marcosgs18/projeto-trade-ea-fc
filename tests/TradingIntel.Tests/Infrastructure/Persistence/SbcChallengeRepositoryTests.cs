using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TradingIntel.Application.Persistence;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;
using TradingIntel.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TradingIntel.Tests.Infrastructure.Persistence;

public sealed class SbcChallengeRepositoryTests : IClassFixture<PersistenceTestFixture>
{
    private readonly PersistenceTestFixture _fixture;

    public SbcChallengeRepositoryTests(PersistenceTestFixture fixture)
    {
        _fixture = fixture;
        // The fixture is shared across tests in this class; reset the SBC
        // tables at the start of each test so list-level assertions on
        // categories/active flags aren't polluted by previous cases.
        using var ctx = _fixture.CreateContext();
        ctx.Database.ExecuteSqlRaw("DELETE FROM sbc_challenge_requirements;");
        ctx.Database.ExecuteSqlRaw("DELETE FROM sbc_challenges;");
    }

    [Fact]
    public async Task UpsertRangeAsync_inserts_new_challenges_with_requirements()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new SbcChallengeRepository(ctx);

        var challenge = NewChallenge(
            title: "Insert me",
            category: "upgrades",
            requirements: new[]
            {
                new SbcRequirement("min_team_rating", 84),
                new SbcRequirement("min_squad_chemistry", 65),
            });

        await repo.UpsertRangeAsync(new[] { challenge }, CancellationToken.None);

        var persisted = await repo.GetByIdAsync(challenge.Id, CancellationToken.None);
        persisted.Should().NotBeNull();
        persisted!.Title.Should().Be("Insert me");
        persisted.Category.Should().Be("upgrades");
        persisted.Requirements.Should().HaveCount(2);
        persisted.Requirements.Select(r => r.Key).Should()
            .ContainInOrder("min_team_rating", "min_squad_chemistry");
        persisted.ObservedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        persisted.ExpiresAtUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task UpsertRangeAsync_is_idempotent_by_id_and_replaces_requirements()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new SbcChallengeRepository(ctx);

        var id = Guid.NewGuid();
        var observedAt = Utc(2026, 4, 22, 10);

        var first = NewChallenge(
            id: id,
            title: "Old title",
            category: "foundations",
            observedAtUtc: observedAt,
            requirements: new[]
            {
                new SbcRequirement("min_team_rating", 82),
                new SbcRequirement("obsolete_key", 1),
            });

        await repo.UpsertRangeAsync(new[] { first }, CancellationToken.None);

        var second = NewChallenge(
            id: id,
            title: "Fresh title",
            category: "upgrades",
            observedAtUtc: observedAt.AddHours(1),
            requirements: new[]
            {
                new SbcRequirement("min_team_rating", 85),
            });

        await repo.UpsertRangeAsync(new[] { second, second }, CancellationToken.None);

        await using var verifyCtx = _fixture.CreateContext();
        var verifyRepo = new SbcChallengeRepository(verifyCtx);
        var persisted = await verifyRepo.GetByIdAsync(id, CancellationToken.None);

        persisted.Should().NotBeNull();
        persisted!.Title.Should().Be("Fresh title");
        persisted.Category.Should().Be("upgrades");
        persisted.ObservedAtUtc.Should().Be(observedAt.AddHours(1));
        persisted.Requirements.Should().HaveCount(1);
        persisted.Requirements[0].Key.Should().Be("min_team_rating");
        persisted.Requirements[0].Minimum.Should().Be(85);
    }

    [Fact]
    public async Task UpsertRangeAsync_empty_is_noop()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new SbcChallengeRepository(ctx);

        await repo.UpsertRangeAsync(Array.Empty<SbcChallenge>(), CancellationToken.None);

        var none = await repo.QueryAsync(new SbcChallengeQuery(), CancellationToken.None);
        none.Should().BeEmpty();
    }

    [Fact]
    public async Task QueryAsync_filters_by_active_expiration()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new SbcChallengeRepository(ctx);

        var nowUtc = Utc(2026, 5, 1, 12);

        var permanent = NewChallenge(title: "Permanent", expiresAtUtc: null);
        var future = NewChallenge(title: "Future", expiresAtUtc: nowUtc.AddDays(3));
        var expired = NewChallenge(title: "Expired", expiresAtUtc: nowUtc.AddDays(-1));

        await repo.UpsertRangeAsync(new[] { permanent, future, expired }, CancellationToken.None);

        var active = await repo.QueryAsync(
            new SbcChallengeQuery { ActiveAsOfUtc = nowUtc },
            CancellationToken.None);

        active.Select(c => c.Title).Should().BeEquivalentTo(new[] { "Permanent", "Future" });
    }

    [Fact]
    public async Task QueryAsync_filters_by_category_contains_case_insensitive()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new SbcChallengeRepository(ctx);

        var a = NewChallenge(title: "A", category: "Foundations");
        var b = NewChallenge(title: "B", category: "Upgrades");
        var c = NewChallenge(title: "C", category: "Advanced Upgrades");

        await repo.UpsertRangeAsync(new[] { a, b, c }, CancellationToken.None);

        var upgrades = await repo.QueryAsync(
            new SbcChallengeQuery { CategoryContains = "upgrades" },
            CancellationToken.None);

        upgrades.Select(x => x.Title).Should().BeEquivalentTo(new[] { "B", "C" });
    }

    [Fact]
    public async Task QueryAsync_filters_by_matches_overall_on_team_rating_keys()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new SbcChallengeRepository(ctx);

        var nowUtc = Utc(2026, 5, 1, 12);

        var needs84 = NewChallenge(
            title: "Needs 84 team",
            expiresAtUtc: nowUtc.AddDays(1),
            requirements: new[] { new SbcRequirement("min_team_rating", 84) });
        var needs86Squad = NewChallenge(
            title: "Needs 86 squad",
            expiresAtUtc: nowUtc.AddDays(1),
            requirements: new[] { new SbcRequirement("Squad_Rating", 86) });
        var chemOnly = NewChallenge(
            title: "Chem only",
            expiresAtUtc: nowUtc.AddDays(1),
            requirements: new[] { new SbcRequirement("min_squad_chemistry", 80) });
        var expired84 = NewChallenge(
            title: "Expired 84",
            expiresAtUtc: nowUtc.AddDays(-1),
            requirements: new[] { new SbcRequirement("min_team_rating", 84) });

        await repo.UpsertRangeAsync(
            new[] { needs84, needs86Squad, chemOnly, expired84 },
            CancellationToken.None);

        var match85 = await repo.QueryAsync(
            new SbcChallengeQuery { ActiveAsOfUtc = nowUtc, MatchesOverall = 85 },
            CancellationToken.None);

        match85.Select(c => c.Title).Should().BeEquivalentTo(new[] { "Needs 84 team" });

        var match86 = await repo.QueryAsync(
            new SbcChallengeQuery { ActiveAsOfUtc = nowUtc, MatchesOverall = 86 },
            CancellationToken.None);

        match86.Select(c => c.Title).Should().BeEquivalentTo(new[] { "Needs 84 team", "Needs 86 squad" });
    }

    private static DateTime Utc(int y, int m, int d, int h) =>
        new(y, m, d, h, 0, 0, DateTimeKind.Utc);

    private static SbcChallenge NewChallenge(
        Guid? id = null,
        string? title = null,
        string? category = null,
        DateTime? expiresAtUtc = null,
        DateTime? observedAtUtc = null,
        IEnumerable<SbcRequirement>? requirements = null)
    {
        var titleValue = title ?? $"Challenge {Guid.NewGuid():N}";
        return new SbcChallenge(
            id: id ?? Guid.NewGuid(),
            title: titleValue,
            category: category ?? "general",
            expiresAtUtc: expiresAtUtc ?? Utc(2026, 5, 1, 12).AddDays(7),
            repeatability: SbcRepeatability.NotRepeatable(),
            setName: "Test Set",
            observedAtUtc: observedAtUtc ?? Utc(2026, 5, 1, 12),
            requirements: requirements ?? new[] { new SbcRequirement("min_team_rating", 80) });
    }
}
