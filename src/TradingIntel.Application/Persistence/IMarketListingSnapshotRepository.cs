using TradingIntel.Domain.Models;

namespace TradingIntel.Application.Persistence;

public interface IMarketListingSnapshotRepository
{
    Task AddRangeAsync(IEnumerable<MarketListingSnapshot> snapshots, CancellationToken cancellationToken);

    Task<IReadOnlyList<MarketListingSnapshot>> GetByPlayerAsync(
        long playerId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken);

    Task<MarketListingSnapshot?> GetByListingIdAsync(string listingId, CancellationToken cancellationToken);

    /// <summary>
    /// Listagens Futbin (<c>futbin:*</c>) na janela temporal.
    /// </summary>
    Task<IReadOnlyList<MarketListingSnapshot>> GetFutbinListingsByPlayerAsync(
        long playerId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<MarketListingSnapshot> Items, int TotalCount)> GetByPlayerPagedAsync(
        long playerId,
        DateTime fromUtc,
        DateTime toUtc,
        int skip,
        int take,
        CancellationToken cancellationToken);
}
