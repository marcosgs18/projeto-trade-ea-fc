using FluentAssertions;
using TradingIntel.Application.Persistence;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;
using TradingIntel.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TradingIntel.Tests.Infrastructure.Persistence;

public sealed class WatchlistRepositoryTests : IClassFixture<PersistenceTestFixture>
{
    private readonly PersistenceTestFixture _fixture;
    private static readonly DateTime AnUtcDate = new(2026, 4, 24, 0, 0, 0, DateTimeKind.Utc);

    public WatchlistRepositoryTests(PersistenceTestFixture fixture)
    {
        _fixture = fixture;
    }

    private WatchlistRepository NewRepository() => new(_fixture.CreateContext());

    private static TrackedPlayer Player(
        long id,
        string name = "Player",
        int? overall = 84,
        WatchlistSource source = WatchlistSource.Seed,
        bool isActive = true,
        DateTime? lastCollectedAtUtc = null) =>
        new(new PlayerReference(id, name), overall, source, AnUtcDate, lastCollectedAtUtc, isActive);

    [Fact]
    public async Task Upsert_inserts_then_updates_display_name_overall_and_active_but_preserves_source_and_added_at()
    {
        var repo = NewRepository();

        var inserted = await repo.UpsertAsync(
            Player(50_001, "Mbappé", overall: 91, source: WatchlistSource.Seed),
            CancellationToken.None);

        inserted.Player.PlayerId.Should().Be(50_001);
        inserted.Source.Should().Be(WatchlistSource.Seed);
        inserted.AddedAtUtc.Should().Be(AnUtcDate);

        var updated = await NewRepository().UpsertAsync(
            new TrackedPlayer(
                new PlayerReference(50_001, "Mbappé FC IF"),
                overall: 95,
                source: WatchlistSource.Api,
                addedAtUtc: AnUtcDate.AddHours(5),
                lastCollectedAtUtc: null,
                isActive: false),
            CancellationToken.None);

        updated.Player.DisplayName.Should().Be("Mbappé FC IF");
        updated.Overall.Should().Be(95);
        updated.IsActive.Should().BeFalse();
        updated.Source.Should().Be(WatchlistSource.Seed);
        updated.AddedAtUtc.Should().Be(AnUtcDate);
    }

    [Fact]
    public async Task GetActive_returns_only_active_players_ordered_by_playerid()
    {
        var seed = NewRepository();
        await seed.UpsertAsync(Player(20_003, "C"), CancellationToken.None);
        await seed.UpsertAsync(Player(20_001, "A"), CancellationToken.None);
        await seed.UpsertAsync(Player(20_002, "B", isActive: false), CancellationToken.None);

        var active = await NewRepository().GetActiveAsync(CancellationToken.None);

        active.Select(p => p.Player.PlayerId).Should().Equal(20_001L, 20_003L);
    }

    [Fact]
    public async Task Deactivate_flips_is_active_and_leaves_row_visible_to_get_by_id()
    {
        await NewRepository().UpsertAsync(Player(30_001), CancellationToken.None);

        var result = await NewRepository().DeactivateAsync(30_001, CancellationToken.None);
        result.Should().BeTrue();

        var fetched = await NewRepository().GetByPlayerIdAsync(30_001, CancellationToken.None);
        fetched.Should().NotBeNull();
        fetched!.IsActive.Should().BeFalse();

        var missing = await NewRepository().DeactivateAsync(99_999, CancellationToken.None);
        missing.Should().BeFalse();
    }

    [Fact]
    public async Task TouchLastCollected_updates_only_matching_ids_and_keeps_utc_kind()
    {
        await NewRepository().UpsertAsync(Player(40_001), CancellationToken.None);
        await NewRepository().UpsertAsync(Player(40_002), CancellationToken.None);
        await NewRepository().UpsertAsync(Player(40_003), CancellationToken.None);

        var stamp = new DateTime(2026, 4, 24, 10, 0, 0, DateTimeKind.Utc);
        await NewRepository().TouchLastCollectedAsync(new[] { 40_001L, 40_003L }, stamp, CancellationToken.None);

        var touched1 = await NewRepository().GetByPlayerIdAsync(40_001, CancellationToken.None);
        var untouched = await NewRepository().GetByPlayerIdAsync(40_002, CancellationToken.None);
        var touched3 = await NewRepository().GetByPlayerIdAsync(40_003, CancellationToken.None);

        touched1!.LastCollectedAtUtc.Should().Be(stamp);
        touched1.LastCollectedAtUtc!.Value.Kind.Should().Be(DateTimeKind.Utc);
        untouched!.LastCollectedAtUtc.Should().BeNull();
        touched3!.LastCollectedAtUtc.Should().Be(stamp);
    }

    [Fact]
    public async Task QueryPaged_filters_by_source_min_overall_and_include_inactive()
    {
        await NewRepository().UpsertAsync(
            Player(60_001, overall: 80, source: WatchlistSource.Seed),
            CancellationToken.None);
        await NewRepository().UpsertAsync(
            Player(60_002, overall: 88, source: WatchlistSource.Api, isActive: false),
            CancellationToken.None);
        await NewRepository().UpsertAsync(
            Player(60_003, overall: 92, source: WatchlistSource.Api),
            CancellationToken.None);

        var api = await NewRepository().QueryPagedAsync(
            new WatchlistQuery(IncludeInactive: false, Source: WatchlistSource.Api, MinOverall: 90),
            skip: 0,
            take: 10,
            CancellationToken.None);

        api.TotalCount.Should().Be(1);
        api.Items.Should().HaveCount(1);
        api.Items[0].Player.PlayerId.Should().Be(60_003);

        var withInactive = await NewRepository().QueryPagedAsync(
            new WatchlistQuery(IncludeInactive: true, Source: WatchlistSource.Api, MinOverall: null),
            skip: 0,
            take: 10,
            CancellationToken.None);

        withInactive.TotalCount.Should().Be(2);
    }
}
