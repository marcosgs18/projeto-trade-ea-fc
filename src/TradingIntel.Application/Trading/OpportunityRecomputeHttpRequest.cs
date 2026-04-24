namespace TradingIntel.Application.Trading;

/// <summary>
/// Corpo opcional de <c>POST /api/opportunities/recompute</c>: filtra a watchlist configurada.
/// </summary>
public sealed class OpportunityRecomputeHttpRequest
{
    public long[]? PlayerIds { get; set; }
}
