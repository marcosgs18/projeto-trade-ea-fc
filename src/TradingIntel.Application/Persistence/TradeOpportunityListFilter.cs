namespace TradingIntel.Application.Persistence;

public sealed record TradeOpportunityListFilter(
    decimal? MinConfidence,
    decimal? MinNetMargin,
    long? PlayerId,
    DateTime? DetectedAfterUtc);
