using TradingIntel.Domain.Models;

namespace TradingIntel.Application.Sbc;

/// <summary>
/// Transforma SBCs ativos em demanda por faixas de overall, com score explicável.
/// </summary>
public interface IRatingBandDemandService
{
    /// <summary>
    /// Calcula a demanda agregada por faixa de rating a partir dos SBCs informados.
    /// </summary>
    /// <param name="challenges">SBCs considerados ativos no momento da análise.</param>
    /// <param name="nowUtc">
    /// Momento de referência (UTC) usado para medir urgência de expiração.
    /// Deve ser <see cref="DateTimeKind.Utc"/>.
    /// </param>
    /// <param name="weights">Pesos da fórmula. Use <see cref="RatingBandDemandWeights.Default"/> se não quiser sintonizar.</param>
    /// <returns>Scores ordenados por relevância decrescente; faixas sem demanda são omitidas.</returns>
    IReadOnlyList<RatingBandDemandScore> ComputeDemand(
        IReadOnlyList<SbcChallenge> challenges,
        DateTime nowUtc,
        RatingBandDemandWeights? weights = null);
}
