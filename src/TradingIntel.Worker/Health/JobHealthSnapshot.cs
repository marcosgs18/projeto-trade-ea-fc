namespace TradingIntel.Worker.Health;

/// <summary>
/// Point-in-time view of a job's execution health used for internal diagnostics
/// and structured logging. Durations are in real wall-clock time measured by the
/// job runner.
/// </summary>
public sealed record JobHealthSnapshot(
    string JobName,
    DateTime? LastSuccessUtc,
    TimeSpan? LastSuccessDuration,
    DateTime? LastFailureUtc,
    TimeSpan? LastFailureDuration,
    string? LastFailureMessage,
    int ConsecutiveFailures,
    DateTime? NextTickUtc);
