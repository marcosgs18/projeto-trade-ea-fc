namespace TradingIntel.Application.Trading;

/// <summary>
/// Recomputa oportunidades de trade para uma lista de jogadores (watchlist), persistindo o resultado.
/// </summary>
public interface IOpportunityRecomputeService
{
    Task<OpportunityRecomputeSummary> RecomputeAsync(
        IReadOnlyList<OpportunityRecomputePlayer> players,
        CancellationToken cancellationToken);
}

/// <summary>
/// Entrada alinhada à watchlist: overall vem do metadata de config (ex.: entrada de watchlist no Worker).
/// </summary>
public sealed record OpportunityRecomputePlayer(long PlayerId, string DisplayName, int? Overall);

public sealed record OpportunityRecomputeSummary(
    int Upserted,
    int RemovedNoEdge,
    int SkippedMissingOverall,
    int SkippedMissingPrice,
    int StaleMarked);
