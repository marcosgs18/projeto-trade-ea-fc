using System.Collections.Concurrent;

namespace TradingIntel.Application.JobHealth;

/// <summary>
/// Default in-memory implementation. Uses a <see cref="ConcurrentDictionary{TKey, TValue}"/>
/// with atomic <see cref="ConcurrentDictionary{TKey,TValue}.AddOrUpdate"/> so that
/// concurrent ticks of independent jobs never block each other.
/// </summary>
public sealed class InMemoryJobHealthRegistry : IJobHealthRegistry
{
    private readonly ConcurrentDictionary<string, JobHealthSnapshot> _snapshots = new(StringComparer.Ordinal);

    public void RecordSuccess(string jobName, DateTime utcNow, TimeSpan duration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);

        _snapshots.AddOrUpdate(
            jobName,
            _ => new JobHealthSnapshot(
                jobName,
                LastSuccessUtc: utcNow,
                LastSuccessDuration: duration,
                LastFailureUtc: null,
                LastFailureDuration: null,
                LastFailureMessage: null,
                ConsecutiveFailures: 0,
                NextTickUtc: null),
            (_, existing) => existing with
            {
                LastSuccessUtc = utcNow,
                LastSuccessDuration = duration,
                ConsecutiveFailures = 0,
            });
    }

    public void RecordFailure(
        string jobName,
        DateTime utcNow,
        TimeSpan duration,
        string errorMessage,
        int consecutiveFailures)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);

        _snapshots.AddOrUpdate(
            jobName,
            _ => new JobHealthSnapshot(
                jobName,
                LastSuccessUtc: null,
                LastSuccessDuration: null,
                LastFailureUtc: utcNow,
                LastFailureDuration: duration,
                LastFailureMessage: errorMessage,
                ConsecutiveFailures: consecutiveFailures,
                NextTickUtc: null),
            (_, existing) => existing with
            {
                LastFailureUtc = utcNow,
                LastFailureDuration = duration,
                LastFailureMessage = errorMessage,
                ConsecutiveFailures = consecutiveFailures,
            });
    }

    public void RecordNextTick(string jobName, DateTime nextTickUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);

        _snapshots.AddOrUpdate(
            jobName,
            _ => new JobHealthSnapshot(
                jobName,
                LastSuccessUtc: null,
                LastSuccessDuration: null,
                LastFailureUtc: null,
                LastFailureDuration: null,
                LastFailureMessage: null,
                ConsecutiveFailures: 0,
                NextTickUtc: nextTickUtc),
            (_, existing) => existing with { NextTickUtc = nextTickUtc });
    }

    public JobHealthSnapshot? Get(string jobName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);
        return _snapshots.TryGetValue(jobName, out var snapshot) ? snapshot : null;
    }

    public IReadOnlyDictionary<string, JobHealthSnapshot> Snapshot()
    {
        return _snapshots
            .ToArray()
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }
}
