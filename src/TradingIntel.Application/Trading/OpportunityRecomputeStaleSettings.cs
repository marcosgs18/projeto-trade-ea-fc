namespace TradingIntel.Application.Trading;

/// <summary>
/// Subconjunto de <c>Jobs:OpportunityRecompute</c> compartilhado entre Worker e API (TTL de obsolescência).
/// </summary>
public sealed class OpportunityRecomputeStaleSettings
{
    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromMinutes(15);
}
