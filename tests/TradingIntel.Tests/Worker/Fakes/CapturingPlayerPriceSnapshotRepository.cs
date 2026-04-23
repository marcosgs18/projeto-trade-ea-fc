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
}
