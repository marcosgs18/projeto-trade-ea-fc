namespace TradingIntel.Worker.Jobs;

public enum TickStatus
{
    Success,
    Failure,
    Cancelled,
}

public sealed record TickResult(TickStatus Status, TimeSpan Elapsed, Exception? Error);
