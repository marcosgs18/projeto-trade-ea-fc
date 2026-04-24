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
    /// Último snapshot do jogador cuja coluna <c>Source</c> começa com
    /// <paramref name="sourcePrefix"/> (ex.: <c>"futgg:"</c> casa
    /// <c>"futgg:pc"</c>, <c>"futgg:console"</c>, …). Permite que o caller
    /// (recompute / scoring) filtre por uma fonte ativa sem conhecer as
    /// plataformas concretas.
    /// </summary>
    Task<PlayerPriceSnapshot?> GetLatestPriceBySourcePrefixAsync(
        long playerId,
        string sourcePrefix,
        CancellationToken cancellationToken);

    /// <summary>
    /// Histórico do jogador na janela <c>[fromUtc, toUtc]</c> filtrando por
    /// snapshots cuja <c>Source</c> começa com <paramref name="sourcePrefix"/>.
    /// </summary>
    Task<IReadOnlyList<PlayerPriceSnapshot>> GetPriceHistoryBySourcePrefixAsync(
        long playerId,
        string sourcePrefix,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<PlayerPriceSnapshot> Items, int TotalCount)> GetByPlayerPagedAsync(
        long playerId,
        string? source,
        DateTime fromUtc,
        DateTime toUtc,
        int skip,
        int take,
        CancellationToken cancellationToken);
}
