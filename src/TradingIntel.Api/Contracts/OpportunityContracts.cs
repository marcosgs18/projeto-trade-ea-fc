namespace TradingIntel.Api.Contracts;

public sealed record OpportunitySummaryResponse(
    Guid Id,
    long PlayerId,
    string PlayerDisplayName,
    DateTime DetectedAtUtc,
    decimal ExpectedBuyPrice,
    decimal ExpectedSellPrice,
    decimal ExpectedNetMargin,
    decimal Confidence,
    bool IsStale,
    DateTime LastRecomputedAtUtc);

public sealed record OpportunityDetailResponse(
    Guid Id,
    long PlayerId,
    string PlayerDisplayName,
    DateTime DetectedAtUtc,
    decimal ExpectedBuyPrice,
    decimal ExpectedSellPrice,
    decimal ExpectedGrossProfit,
    decimal ExpectedNetMargin,
    decimal Confidence,
    bool IsStale,
    DateTime LastRecomputedAtUtc,
    IReadOnlyList<OpportunityReasonResponse> Reasons,
    IReadOnlyList<ExecutionSuggestionResponse> Suggestions);

public sealed record OpportunityReasonResponse(string Code, string Message, decimal Weight);

public sealed record ExecutionSuggestionResponse(
    Guid Id,
    Guid OpportunityId,
    string Action,
    decimal TargetPrice,
    DateTime ValidUntilUtc);

public sealed record OpportunityRecomputeResponse(
    int Upserted,
    int RemovedNoEdge,
    int SkippedMissingOverall,
    int SkippedMissingPrice,
    int StaleMarked);
