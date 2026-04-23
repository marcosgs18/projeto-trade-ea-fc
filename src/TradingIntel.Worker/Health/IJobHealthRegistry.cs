namespace TradingIntel.Worker.Health;

/// <summary>
/// Singleton, thread-safe registry that records the outcome of each scheduled
/// job tick. Lives in memory only; persistence can be layered later without
/// changing the contract.
/// </summary>
public interface IJobHealthRegistry
{
    void RecordSuccess(string jobName, DateTime utcNow, TimeSpan duration);

    void RecordFailure(
        string jobName,
        DateTime utcNow,
        TimeSpan duration,
        string errorMessage,
        int consecutiveFailures);

    void RecordNextTick(string jobName, DateTime nextTickUtc);

    JobHealthSnapshot? Get(string jobName);

    IReadOnlyDictionary<string, JobHealthSnapshot> Snapshot();
}
