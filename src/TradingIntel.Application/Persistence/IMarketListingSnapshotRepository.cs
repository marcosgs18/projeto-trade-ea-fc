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
    /// Listagens do jogador na janela <c>[fromUtc, toUtc]</c> filtrando por
    /// snapshots cuja <c>Source</c> começa com <paramref name="sourcePrefix"/>
    /// (ex.: <c>"futgg:"</c>, <c>"futbin:"</c>).
    /// </summary>
    Task<IReadOnlyList<MarketListingSnapshot>> GetListingsByPlayerBySourcePrefixAsync(
        long playerId,
        string sourcePrefix,
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
