using TradingIntel.Domain.Models;

namespace TradingIntel.Application.Persistence;

/// <summary>
/// Persistência da watchlist de jogadores rastreados pela coleta de preços e
/// pelo recompute de oportunidades. Substitui a leitura direta de
/// <c>Jobs:PriceCollection:Players</c> em <c>appsettings</c>.
/// </summary>
/// <remarks>
/// O contrato é deliberadamente pequeno porque o caminho de leitura é quente
/// (cada tick do <c>price-collection</c> chama <see cref="GetActiveAsync"/>).
/// Updates devem ser idempotentes e raros — admin via API ou seed na boot.
/// </remarks>
public interface IWatchlistRepository
{
    /// <summary>
    /// Retorna todos os jogadores ativos. Ordenado por <c>PlayerId</c> para
    /// consistência entre ticks (logs comparáveis, batch determinístico).
    /// </summary>
    Task<IReadOnlyList<TrackedPlayer>> GetActiveAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Lookup pontual por <c>PlayerId</c> (eaId). Retorna entradas inativas
    /// também — útil para reativar via API sem perder o histórico de
    /// <c>AddedAtUtc</c>/<c>Source</c>.
    /// </summary>
    Task<TrackedPlayer?> GetByPlayerIdAsync(long playerId, CancellationToken cancellationToken);

    /// <summary>
    /// Insere se ainda não existe; quando já existe, atualiza apenas
    /// <c>DisplayName</c>, <c>Overall</c> e <c>IsActive</c> — preserva
    /// <c>Source</c> e <c>AddedAtUtc</c> originais para preservar a trilha
    /// de auditoria. Use <see cref="TouchLastCollectedAsync"/> para o
    /// timestamp de coleta.
    /// </summary>
    Task<TrackedPlayer> UpsertAsync(TrackedPlayer player, CancellationToken cancellationToken);

    /// <summary>
    /// Soft-delete: marca <c>IsActive = false</c>. Retorna <c>false</c> se
    /// não houver linha para o <paramref name="playerId"/>.
    /// </summary>
    Task<bool> DeactivateAsync(long playerId, CancellationToken cancellationToken);

    /// <summary>
    /// Atualiza <c>LastCollectedAtUtc</c> para um conjunto de
    /// <paramref name="playerIds"/>. Operação em massa (uma única ida ao
    /// banco) para o final do tick do <c>price-collection</c>.
    /// </summary>
    Task TouchLastCollectedAsync(
        IReadOnlyCollection<long> playerIds,
        DateTime collectedAtUtc,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<TrackedPlayer> Items, int TotalCount)> QueryPagedAsync(
        WatchlistQuery query,
        int skip,
        int take,
        CancellationToken cancellationToken);
}
