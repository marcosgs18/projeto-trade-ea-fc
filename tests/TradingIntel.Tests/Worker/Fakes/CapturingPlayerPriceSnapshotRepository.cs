using TradingIntel.Application.Persistence;
using TradingIntel.Domain.Models;

namespace TradingIntel.Tests.Worker.Fakes;

internal sealed class CapturingPlayerPriceSnapshotRepository : IPlayerPriceSnapshotRepository
{
    public List<PlayerPriceSnapshot> Saved { get; } = new();

    public Task AddRangeAsync(IEnumerable<PlayerPriceSnapshot> snapshots, CancellationToken cancellationToken)
    {
        Saved.AddRange(snapshots);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PlayerPriceSnapshot>> GetByPlayerAsync(long playerId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<PlayerPriceSnapshot>>(Array.Empty<PlayerPriceSnapshot>());

    public Task<PlayerPriceSnapshot?> GetLatestForPlayerAsync(long playerId, string source, CancellationToken cancellationToken)
        => Task.FromResult<PlayerPriceSnapshot?>(null);

    public Task<PlayerPriceSnapshot?> GetLatestPriceBySourcePrefixAsync(
        long playerId,
        string sourcePrefix,
        CancellationToken cancellationToken)
    {
        var latest = Saved
            .Where(s => s.Player.PlayerId == playerId && s.Source.StartsWith(sourcePrefix, StringComparison.Ordinal))
            .OrderByDescending(s => s.CapturedAtUtc)
            .FirstOrDefault();
        return Task.FromResult<PlayerPriceSnapshot?>(latest);
    }

    public Task<IReadOnlyList<PlayerPriceSnapshot>> GetPriceHistoryBySourcePrefixAsync(
        long playerId,
        string sourcePrefix,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var list = Saved
            .Where(s =>
                s.Player.PlayerId == playerId
                && s.Source.StartsWith(sourcePrefix, StringComparison.Ordinal)
                && s.CapturedAtUtc >= fromUtc
                && s.CapturedAtUtc <= toUtc)
            .OrderBy(s => s.CapturedAtUtc)
            .ToList();
        return Task.FromResult<IReadOnlyList<PlayerPriceSnapshot>>(list);
    }

    public Task<(IReadOnlyList<PlayerPriceSnapshot> Items, int TotalCount)> GetByPlayerPagedAsync(
        long playerId,
        string? source,
        DateTime fromUtc,
        DateTime toUtc,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = Saved
            .Where(s =>
                s.Player.PlayerId == playerId
                && s.CapturedAtUtc >= fromUtc
                && s.CapturedAtUtc <= toUtc
                && (string.IsNullOrWhiteSpace(source) || s.Source == source))
            .OrderBy(s => s.CapturedAtUtc)
            .ToList();
        var total = query.Count;
        var slice = query.Skip(skip).Take(take).ToList();
        return Task.FromResult<(IReadOnlyList<PlayerPriceSnapshot> Items, int TotalCount)>((slice, total));
    }
}

internal sealed class CapturingMarketListingSnapshotRepository : IMarketListingSnapshotRepository
{
    public List<MarketListingSnapshot> Saved { get; } = new();

    public Task AddRangeAsync(IEnumerable<MarketListingSnapshot> snapshots, CancellationToken cancellationToken)
    {
        Saved.AddRange(snapshots);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MarketListingSnapshot>> GetByPlayerAsync(long playerId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<MarketListingSnapshot>>(Array.Empty<MarketListingSnapshot>());

    public Task<MarketListingSnapshot?> GetByListingIdAsync(string listingId, CancellationToken cancellationToken)
        => Task.FromResult<MarketListingSnapshot?>(null);

    public Task<IReadOnlyList<MarketListingSnapshot>> GetListingsByPlayerBySourcePrefixAsync(
        long playerId,
        string sourcePrefix,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var list = Saved
            .Where(s =>
                s.Player.PlayerId == playerId
                && s.Source.StartsWith(sourcePrefix, StringComparison.Ordinal)
                && s.CapturedAtUtc >= fromUtc
                && s.CapturedAtUtc <= toUtc)
            .OrderBy(s => s.CapturedAtUtc)
            .ToList();
        return Task.FromResult<IReadOnlyList<MarketListingSnapshot>>(list);
    }

    public Task<(IReadOnlyList<MarketListingSnapshot> Items, int TotalCount)> GetByPlayerPagedAsync(
        long playerId,
        DateTime fromUtc,
        DateTime toUtc,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = Saved
            .Where(s => s.Player.PlayerId == playerId && s.CapturedAtUtc >= fromUtc && s.CapturedAtUtc <= toUtc)
            .OrderBy(s => s.CapturedAtUtc)
            .ToList();
        var total = query.Count;
        var slice = query.Skip(skip).Take(take).ToList();
        return Task.FromResult<(IReadOnlyList<MarketListingSnapshot> Items, int TotalCount)>((slice, total));
    }
}
