using TradingIntel.Domain.Models;

namespace TradingIntel.Application.Persistence;

public interface IPlayerPriceSnapshotRepository
{
    Task AddRangeAsync(IEnumerable<PlayerPriceSnapshot> snapshots, CancellationToken cancellationToken);

    Task<IReadOnlyList<PlayerPriceSnapshot>> GetByPlayerAsync(
        long playerId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken);

    Task<PlayerPriceSnapshot?> GetLatestForPlayerAsync(
        long playerId,
        string source,
        CancellationToken cancellationToken);
}
