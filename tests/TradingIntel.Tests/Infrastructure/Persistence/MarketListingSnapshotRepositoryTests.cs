using FluentAssertions;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;
using TradingIntel.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TradingIntel.Tests.Infrastructure.Persistence;

public sealed class MarketListingSnapshotRepositoryTests : IClassFixture<PersistenceTestFixture>
{
    private readonly PersistenceTestFixture _fixture;

    public MarketListingSnapshotRepositoryTests(PersistenceTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AddRangeAsync_and_GetByPlayerAsync_support_temporal_queries()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new MarketListingSnapshotRepository(ctx);

        var player = new PlayerReference(7_001, "Listing Player");
        var t1 = new DateTime(2026, 04, 22, 10, 0, 0, DateTimeKind.Utc);
        var t2 = t1.AddHours(1);
        var t3 = t1.AddHours(3);

        await repo.AddRangeAsync(new[]
        {
            Build(player, "listing-a", t1, 1_500_000),
            Build(player, "listing-b", t2, 1_510_000),
            Build(player, "listing-c", t3, 1_495_000),
        }, CancellationToken.None);

        var window = await repo.GetByPlayerAsync(player.PlayerId, t1, t2, CancellationToken.None);
        window.Should().HaveCount(2);
        window.Select(w => w.ListingId).Should().ContainInOrder("listing-a", "listing-b");
        window.Should().AllSatisfy(w => w.CapturedAtUtc.Kind.Should().Be(DateTimeKind.Utc));
    }

    [Fact]
    public async Task GetByListingIdAsync_returns_single_listing_when_persisted()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new MarketListingSnapshotRepository(ctx);

        var player = new PlayerReference(7_002, "Listing Unique");
        var capturedAt = new DateTime(2026, 04, 22, 16, 0, 0, DateTimeKind.Utc);
        var listingId = $"unique-{Guid.NewGuid():N}";

        await repo.AddRangeAsync(new[] { Build(player, listingId, capturedAt, 250_000) }, CancellationToken.None);

        var found = await repo.GetByListingIdAsync(listingId, CancellationToken.None);
        found.Should().NotBeNull();
        found!.ListingId.Should().Be(listingId);
        found.BuyNowPrice.Value.Should().Be(250_000);
        found.ExpiresAtUtc.Should().BeAfter(found.CapturedAtUtc);
    }

    [Fact]
    public async Task GetByListingIdAsync_returns_null_when_not_found()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new MarketListingSnapshotRepository(ctx);

        var result = await repo.GetByListingIdAsync($"missing-{Guid.NewGuid():N}", CancellationToken.None);

        result.Should().BeNull();
    }

    private static MarketListingSnapshot Build(PlayerReference player, string listingId, DateTime capturedAtUtc, decimal price)
    {
        var coins = new Coins(price);
        return new MarketListingSnapshot(
            listingId,
            player,
            "futbin:ps",
            capturedAtUtc,
            startingBid: coins,
            buyNowPrice: coins,
            expiresAtUtc: capturedAtUtc.AddMinutes(10));
    }
}
