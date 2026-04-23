using FluentAssertions;
using TradingIntel.Domain.Models;
using TradingIntel.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TradingIntel.Tests.Infrastructure.Persistence;

public sealed class SqliteRawSnapshotStoreTests : IClassFixture<PersistenceTestFixture>
{
    private readonly PersistenceTestFixture _fixture;

    public SqliteRawSnapshotStoreTests(PersistenceTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SaveAsync_and_GetBySourceAsync_round_trip_snapshots_by_time_range()
    {
        await using var ctx = _fixture.CreateContext();
        var store = new SqliteRawSnapshotStore(ctx);

        var source = $"source-{Guid.NewGuid():N}";
        var t1 = new DateTime(2026, 04, 22, 10, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 04, 22, 11, 0, 0, DateTimeKind.Utc);
        var t3 = new DateTime(2026, 04, 22, 12, 0, 0, DateTimeKind.Utc);

        await store.SaveAsync(BuildMetadata(source, t1, "hash-1"), "payload-1", CancellationToken.None);
        await store.SaveAsync(BuildMetadata(source, t2, "hash-2"), "payload-2", CancellationToken.None);
        await store.SaveAsync(BuildMetadata(source, t3, "hash-3"), "payload-3", CancellationToken.None);

        var slice = await store.GetBySourceAsync(source, t1, t2, CancellationToken.None);
        slice.Should().HaveCount(2);
        slice.Select(s => s.RawPayload).Should().ContainInOrder("payload-1", "payload-2");
        slice.Should().AllSatisfy(s =>
        {
            s.Metadata.Source.Should().Be(source);
            s.Metadata.CapturedAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        });
    }

    [Fact]
    public async Task GetLatestAsync_returns_most_recent_for_source()
    {
        await using var ctx = _fixture.CreateContext();
        var store = new SqliteRawSnapshotStore(ctx);

        var source = $"latest-{Guid.NewGuid():N}";
        await store.SaveAsync(BuildMetadata(source, new DateTime(2026, 04, 22, 10, 0, 0, DateTimeKind.Utc), "h1"), "p1", CancellationToken.None);
        await store.SaveAsync(BuildMetadata(source, new DateTime(2026, 04, 22, 13, 0, 0, DateTimeKind.Utc), "h2"), "p2", CancellationToken.None);
        await store.SaveAsync(BuildMetadata(source, new DateTime(2026, 04, 22, 11, 30, 0, DateTimeKind.Utc), "h3"), "p3", CancellationToken.None);

        var latest = await store.GetLatestAsync(source, CancellationToken.None);

        latest.Should().NotBeNull();
        latest!.RawPayload.Should().Be("p2");
        latest.Metadata.CapturedAtUtc.Should().Be(new DateTime(2026, 04, 22, 13, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task GetLatestAsync_returns_null_when_source_unknown()
    {
        await using var ctx = _fixture.CreateContext();
        var store = new SqliteRawSnapshotStore(ctx);

        var result = await store.GetLatestAsync($"unknown-{Guid.NewGuid():N}", CancellationToken.None);

        result.Should().BeNull();
    }

    private static SourceSnapshotMetadata BuildMetadata(string source, DateTime capturedAtUtc, string hash) =>
        new(source, capturedAtUtc, recordCount: 1, correlationId: Guid.NewGuid().ToString("N"), payloadHash: hash);
}
