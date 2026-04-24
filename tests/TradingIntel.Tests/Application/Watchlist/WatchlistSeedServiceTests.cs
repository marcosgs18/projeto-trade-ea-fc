using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradingIntel.Application.Watchlist;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;
using TradingIntel.Tests.Worker.Fakes;
using Xunit;

namespace TradingIntel.Tests.Application.Watchlist;

public sealed class WatchlistSeedServiceTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly InMemoryWatchlistRepository _watchlist;
    private readonly FakeClock _clock = new(new DateTime(2026, 4, 24, 10, 0, 0, DateTimeKind.Utc));

    public WatchlistSeedServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "watchlist-seed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _watchlist = new InMemoryWatchlistRepository();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { /* best-effort */ }
    }

    private string WriteCatalog(object payload)
    {
        var path = Path.Combine(_tempRoot, "catalog.json");
        File.WriteAllText(path, JsonSerializer.Serialize(payload));
        return path;
    }

    private WatchlistSeedService BuildService(string? catalogPath, bool requireCatalog = false)
    {
        var options = Options.Create(new WatchlistSeedOptions
        {
            CatalogSeedPath = catalogPath ?? string.Empty,
            RequireCatalogSeed = requireCatalog,
        });
        return new WatchlistSeedService(_watchlist, options, _clock, NullLogger<WatchlistSeedService>.Instance);
    }

    [Fact]
    public async Task Inserts_new_seed_entries_and_merges_appsettings_with_precedence()
    {
        var catalogPath = WriteCatalog(new
        {
            version = 1,
            players = new[]
            {
                new { playerId = 50_001, displayName = "Seed Mbappé", overall = 91 },
                new { playerId = 50_002, displayName = "Seed Haaland", overall = 90 },
            },
        });

        var service = BuildService(catalogPath);

        var report = await service.SeedAsync(
            new[]
            {
                // Same id as seed #1, but appsettings wins → display name changes.
                new WatchlistSeedEntry(50_001, "AppSettings Mbappé", 92, WatchlistSource.AppSettings),
                new WatchlistSeedEntry(50_003, "AppSettings Only", null, WatchlistSource.AppSettings),
            },
            CancellationToken.None);

        report.CatalogEntriesRead.Should().Be(2);
        report.AppSettingsEntriesRead.Should().Be(2);
        report.Inserted.Should().Be(3);
        report.Skipped.Should().Be(0);

        var all = await _watchlist.GetActiveAsync(CancellationToken.None);
        all.Should().HaveCount(3);

        var mbappe = all.Single(p => p.Player.PlayerId == 50_001);
        mbappe.Player.DisplayName.Should().Be("AppSettings Mbappé");
        mbappe.Source.Should().Be(WatchlistSource.AppSettings);
        mbappe.Overall.Should().Be(92);
    }

    [Fact]
    public async Task Second_run_is_idempotent_preserves_added_at_and_never_downgrades_api_source()
    {
        var catalogPath = WriteCatalog(new
        {
            version = 1,
            players = new[] { new { playerId = 70_001, displayName = "From Seed", overall = 85 } },
        });

        var addedByApiAt = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);
        await _watchlist.UpsertAsync(
            new TrackedPlayer(
                new PlayerReference(70_001, "API Name"),
                overall: 90,
                source: WatchlistSource.Api,
                addedAtUtc: addedByApiAt,
                lastCollectedAtUtc: null,
                isActive: true),
            CancellationToken.None);

        var service = BuildService(catalogPath);
        var report1 = await service.SeedAsync(Array.Empty<WatchlistSeedEntry>(), CancellationToken.None);
        var report2 = await service.SeedAsync(Array.Empty<WatchlistSeedEntry>(), CancellationToken.None);

        report1.Inserted.Should().Be(0);
        report2.Inserted.Should().Be(0);

        var row = (await _watchlist.GetByPlayerIdAsync(70_001, CancellationToken.None))!;
        row.Source.Should().Be(WatchlistSource.Api);
        row.AddedAtUtc.Should().Be(addedByApiAt);
        row.Player.DisplayName.Should().Be("From Seed");
    }

    [Fact]
    public async Task Missing_catalog_file_is_tolerated_when_not_required()
    {
        var missing = Path.Combine(_tempRoot, "does-not-exist.json");
        var service = BuildService(missing, requireCatalog: false);

        var report = await service.SeedAsync(
            new[] { new WatchlistSeedEntry(80_001, "X", null, WatchlistSource.AppSettings) },
            CancellationToken.None);

        report.CatalogEntriesRead.Should().Be(0);
        report.Inserted.Should().Be(1);
    }

    [Fact]
    public async Task Missing_catalog_file_raises_when_required()
    {
        var missing = Path.Combine(_tempRoot, "does-not-exist.json");
        var service = BuildService(missing, requireCatalog: true);

        Func<Task> act = () => service.SeedAsync(Array.Empty<WatchlistSeedEntry>(), CancellationToken.None);
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task Invalid_entries_are_counted_as_skipped()
    {
        var catalogPath = WriteCatalog(new
        {
            version = 1,
            players = new[] { new { playerId = 0, displayName = "bad", overall = 10 } },
        });

        var service = BuildService(catalogPath);

        var report = await service.SeedAsync(
            new[]
            {
                new WatchlistSeedEntry(90_001, "  ", null, WatchlistSource.AppSettings),
            },
            CancellationToken.None);

        report.Skipped.Should().Be(2);
        report.Inserted.Should().Be(0);
    }

    private sealed class FakeClock : TimeProvider
    {
        private DateTimeOffset _now;
        public FakeClock(DateTime utcNow) => _now = new DateTimeOffset(utcNow, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
