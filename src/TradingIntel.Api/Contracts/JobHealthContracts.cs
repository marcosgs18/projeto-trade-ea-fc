namespace TradingIntel.Api.Contracts;

public sealed record JobsHealthResponse(IReadOnlyList<JobHealthEntry> Jobs);

public sealed record JobHealthEntry(
    string JobName,
    DateTime? LastSuccessUtc,
    double? LastSuccessDurationMs,
    DateTime? LastFailureUtc,
    double? LastFailureDurationMs,
    string? LastFailureMessage,
    int ConsecutiveFailures,
    DateTime? NextTickUtc);
