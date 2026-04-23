using TradingIntel.Application.FutGg;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;

namespace TradingIntel.Tests.Worker.Fakes;

internal sealed class FakeFutGgSbcClient : IFutGgSbcClient
{
    public int CallCount { get; private set; }

    public Func<CancellationToken, Task<FutGgSbcListingSnapshot>>? Handler { get; set; }

    public Task<FutGgSbcListingSnapshot> GetSbcListingSnapshotAsync(CancellationToken cancellationToken)
    {
        CallCount++;
        if (Handler is null)
        {
            return Task.FromResult(BuildDefaultSnapshot());
        }

        return Handler(cancellationToken);
    }

    public static FutGgSbcListingSnapshot BuildDefaultSnapshot(int challengeCount = 1)
    {
        var capturedAt = new DateTime(2026, 04, 22, 12, 0, 0, DateTimeKind.Utc);

        var challenges = Enumerable.Range(0, challengeCount)
            .Select(i => new SbcChallenge(
                id: Guid.NewGuid(),
                title: $"Challenge {i}",
                category: "upgrades",
                expiresAtUtc: capturedAt.AddDays(7),
                repeatability: SbcRepeatability.NotRepeatable(),
                setName: "Test Set",
                observedAtUtc: capturedAt,
                requirements: new[]
                {
                    new SbcRequirement("min_team_rating", minimum: 83),
                }))
            .ToArray();

        return new FutGgSbcListingSnapshot(
            Source: "futgg",
            CapturedAtUtc: capturedAt,
            CorrelationId: Guid.NewGuid().ToString("N"),
            RawPayload: "raw-fixture",
            Challenges: challenges);
    }
}
