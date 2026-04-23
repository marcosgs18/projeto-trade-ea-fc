using TradingIntel.Domain.Models;

namespace TradingIntel.Application.Trading;

/// <summary>
/// Serviço de scoring de oportunidades de trade (V1).
/// </summary>
/// <remarks>
/// A V1 é uma fórmula linear e transparente baseada em cinco fatores: demanda por overall,
/// spread líquido após taxa de 5%, liquidez observada, estabilidade de preço (inverso da
/// volatilidade) e urgência por expiração de SBC. Sem ML nesta fase.
/// </remarks>
public interface ITradeScoringService
{
    /// <summary>
    /// Calcula a oportunidade de trade para um jogador.
    /// </summary>
    /// <param name="input">Contexto completo (preço, histórico, listagens, demanda).</param>
    /// <param name="weights">Pesos da fórmula. Use <see cref="TradeScoringWeights.Default"/> se não quiser sintonizar.</param>
    /// <returns>
    /// Uma <see cref="TradeOpportunity"/> quando há edge líquido (margem líquida &gt; 0 após taxa de 5%);
    /// <c>null</c> quando não existe edge — nesse caso a decisão é "não operar".
    /// </returns>
    TradeOpportunity? Score(TradeScoringInput input, TradeScoringWeights? weights = null);
}
