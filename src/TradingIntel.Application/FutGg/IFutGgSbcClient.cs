using TradingIntel.Domain.Models;

namespace TradingIntel.Application.FutGg;

public interface IFutGgSbcClient
{
    Task<FutGgSbcListingSnapshot> GetSbcListingSnapshotAsync(CancellationToken cancellationToken);
}

public sealed record FutGgSbcListingSnapshot(
    string Source,
    DateTime CapturedAtUtc,
    string CorrelationId,
    string RawPayload,
    IReadOnlyList<SbcChallenge> Challenges);

