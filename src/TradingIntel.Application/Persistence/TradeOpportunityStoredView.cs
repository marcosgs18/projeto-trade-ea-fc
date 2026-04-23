using TradingIntel.Domain.Models;

namespace TradingIntel.Application.Persistence;

/// <summary>
/// Oportunidade materializada do armazém com metadados de recomputação.
/// </summary>
public sealed record TradeOpportunityStoredView(
    TradeOpportunity Opportunity,
    DateTime LastRecomputedAtUtc,
    bool IsStale);
