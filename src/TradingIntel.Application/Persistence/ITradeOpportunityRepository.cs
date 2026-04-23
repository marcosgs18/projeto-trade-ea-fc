using TradingIntel.Domain.Models;

namespace TradingIntel.Application.Persistence;

/// <summary>
/// Persistência da última <see cref="TradeOpportunity"/> conhecida por jogador (upsert por <c>PlayerId</c>).
/// </summary>
public interface ITradeOpportunityRepository
{
    Task UpsertAsync(TradeOpportunity opportunity, DateTime lastRecomputedAtUtc, CancellationToken cancellationToken);

    Task DeleteByPlayerIdAsync(long playerId, CancellationToken cancellationToken);

    /// <summary>
    /// Marca como obsoletas as linhas cuja última recomputação é anterior a <paramref name="cutoffUtc"/>.
    /// </summary>
    /// <returns>Quantidade de linhas afetadas.</returns>
    Task<int> MarkStaleWhereLastRecomputedBeforeAsync(DateTime cutoffUtc, CancellationToken cancellationToken);

    Task<bool> ExistsForPlayerAsync(long playerId, CancellationToken cancellationToken);

    Task<(IReadOnlyList<TradeOpportunityStoredView> Items, int TotalCount)> QueryPagedAsync(
        TradeOpportunityListFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken);

    Task<TradeOpportunityStoredView?> GetByOpportunityIdAsync(Guid opportunityId, CancellationToken cancellationToken);
}
