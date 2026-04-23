using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingIntel.Application.FutGg;
using TradingIntel.Worker.Health;

namespace TradingIntel.Worker.Jobs;

/// <summary>
/// Collects the FUT.GG active SBC listing. Raw payload persistence is handled
/// inside <see cref="IFutGgSbcClient"/> (it writes to <c>IRawSnapshotStore</c>
/// before returning), so this job only needs to invoke the client and observe
/// the normalized result. V1 does not persist <c>SbcChallenge</c> instances
/// (no repository yet); that is tracked for a follow-up task.
/// </summary>
public sealed class SbcCollectionJob : ScheduledJob
{
    public const string Name = "sbc-collection";

    public SbcCollectionJob(
        IOptions<SbcCollectionOptions> options,
        IServiceScopeFactory scopeFactory,
        IJobHealthRegistry health,
        ILogger<SbcCollectionJob> logger)
        : base(Name, options.Value, scopeFactory, health, logger)
    {
    }

    protected override async Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var client = serviceProvider.GetRequiredService<IFutGgSbcClient>();

        var snapshot = await client
            .GetSbcListingSnapshotAsync(cancellationToken)
            .ConfigureAwait(false);

        Logger.LogInformation(
            "{Job} collected {ChallengeCount} SBC challenges from {Source}. correlationId={CorrelationId} capturedAt={CapturedAtUtc:O}",
            Name,
            snapshot.Challenges.Count,
            snapshot.Source,
            snapshot.CorrelationId,
            snapshot.CapturedAtUtc);
    }
}

public sealed class SbcCollectionOptions : JobScheduleOptions
{
}
