using FluentAssertions;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;
using TradingIntel.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TradingIntel.Tests.Infrastructure.Persistence;

public sealed class PlayerPriceSnapshotRepositoryTests : IClassFixture<PersistenceTestFixture>
{
    private readonly PersistenceTestFixture _fixture;

    public PlayerPriceSnapshotRepositoryTests(PersistenceTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AddRangeAsync_and_GetByPlayerAsync_support_temporal_queries()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new PlayerPriceSnapshotRepository(ctx);

        var player = new PlayerReference(playerId: 1_000 + Random.Shared.Next(1_000), "Player Prices");
        var t1 = new DateTime(2026, 04, 22, 10, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 04, 22, 11, 0, 0, DateTimeKind.Utc);
        var t3 = new DateTime(2026, 04, 22, 12, 0, 0, DateTimeKind.Utc);

        await repo.AddRangeAsync(new[]
        {
            new PlayerPriceSnapshot(player, "futbin:ps", t1, new Coins(1_500_000), new Coins(1_510_000), new Coins(1_505_000)),
            new PlayerPriceSnapshot(player, "futbin:ps", t2, new Coins(1_520_000), null,                     new Coins(1_520_000)),
            new PlayerPriceSnapshot(player, "futbin:ps", t3, new Coins(1_490_000), new Coins(1_495_000), new Coins(1_492_500)),
        }, CancellationToken.None);

        var window = await repo.GetByPlayerAsync(player.PlayerId, t1, t2, CancellationToken.None);

        window.Should().HaveCount(2);
        window[0].CapturedAtUtc.Should().Be(t1);
        window[0].CapturedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        window[1].BuyNowPrice.Value.Should().Be(1_520_000);
        window[1].SellNowPrice.Should().BeNull();
    }

    [Fact]
    public async Task GetLatestForPlayerAsync_returns_most_recent_snapshot_for_source()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new PlayerPriceSnapshotRepository(ctx);

        var player = new PlayerReference(42_424, "Latest Player");
        var earlier = new DateTime(2026, 04, 22, 10, 0, 0, DateTimeKind.Utc);
        var later = new DateTime(2026, 04, 22, 15, 0, 0, DateTimeKind.Utc);

        await repo.AddRangeAsync(new[]
        {
            new PlayerPriceSnapshot(player, "futbin:ps", earlier, new Coins(100_000), null, new Coins(100_000)),
            new PlayerPriceSnapshot(player, "futbin:ps", later,   new Coins(110_000), null, new Coins(110_000)),
            new PlayerPriceSnapshot(player, "futbin:xbox", later, new Coins(200_000), null, new Coins(200_000)),
        }, CancellationToken.None);

        var latestPs = await repo.GetLatestForPlayerAsync(player.PlayerId, "futbin:ps", CancellationToken.None);
        latestPs.Should().NotBeNull();
        latestPs!.BuyNowPrice.Value.Should().Be(110_000);
        latestPs.CapturedAtUtc.Should().Be(later);
    }

    [Fact]
    public async Task AddRangeAsync_empty_enumerable_is_noop()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new PlayerPriceSnapshotRepository(ctx);

        await repo.AddRangeAsync(Array.Empty<PlayerPriceSnapshot>(), CancellationToken.None);

        var none = await repo.GetByPlayerAsync(
            playerId: 9_999_999,
            fromUtc: DateTime.UtcNow.AddDays(-1),
            toUtc: DateTime.UtcNow,
            CancellationToken.None);

        none.Should().BeEmpty();
    }
}
