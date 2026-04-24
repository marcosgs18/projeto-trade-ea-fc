using TradingIntel.Api.Contracts;
using TradingIntel.Application.JobHealth;

namespace TradingIntel.Api.Mapping;

internal static class JobHealthMapper
{
    public static JobsHealthResponse ToResponse(IReadOnlyDictionary<string, JobHealthSnapshot> snapshot)
    {
        var list = snapshot.Values
            .Select(s => new JobHealthEntry(
                s.JobName,
                s.LastSuccessUtc,
                s.LastSuccessDuration?.TotalMilliseconds,
                s.LastFailureUtc,
                s.LastFailureDuration?.TotalMilliseconds,
                s.LastFailureMessage,
                s.ConsecutiveFailures,
                s.NextTickUtc))
            .OrderBy(e => e.JobName, StringComparer.Ordinal)
            .ToList();

        return new JobsHealthResponse(list);
    }
}
