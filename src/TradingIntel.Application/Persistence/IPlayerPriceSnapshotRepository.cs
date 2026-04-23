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

    /// <summary>
    /// Último snapshot Futbin para o jogador (qualquer plataforma: fonte com prefixo <c>futbin:</c>).
    /// </summary>
    Task<PlayerPriceSnapshot?> GetLatestFutbinPriceForPlayerAsync(long playerId, CancellationToken cancellationToken);

    /// <summary>
    /// Histórico Futbin (<c>futbin:*</c>) na janela temporal.
    /// </summary>
    Task<IReadOnlyList<PlayerPriceSnapshot>> GetFutbinPriceHistoryAsync(
        long playerId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken);
}
