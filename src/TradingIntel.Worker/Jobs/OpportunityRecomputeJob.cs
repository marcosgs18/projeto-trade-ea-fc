using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingIntel.Application.JobHealth;
using TradingIntel.Application.Persistence;
using TradingIntel.Application.Trading;

namespace TradingIntel.Worker.Jobs;

/// <summary>
/// Recomputa oportunidades de trade para a watchlist persistida em
/// <c>tracked_players</c> e aplica TTL de obsolescência nas linhas resultantes.
/// </summary>
public sealed class OpportunityRecomputeJob : ScheduledJob
{
    public const string Name = "opportunity-recompute";

    public OpportunityRecomputeJob(
        IOptions<OpportunityRecomputeOptions> options,
        IServiceScopeFactory scopeFactory,
        IJobHealthRegistry health,
        ILogger<OpportunityRecomputeJob> logger)
        : base(Name, options.Value, scopeFactory, health, logger)
    {
    }

    protected override async Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var watchlist = serviceProvider.GetRequiredService<IWatchlistRepository>();
        var players = await watchlist.GetActiveAsync(cancellationToken).ConfigureAwait(false);

        if (players.Count == 0)
        {
            Logger.LogWarning(
                "{Job} watchlist is empty (tracked_players has no active rows); nothing to recompute.",
                Name);
            return;
        }

        var batch = players
            .Select(p => new OpportunityRecomputePlayer(p.Player.PlayerId, p.Player.DisplayName, p.Overall))
            .ToList();

        var recompute = serviceProvider.GetRequiredService<IOpportunityRecomputeService>();

        await recompute
            .RecomputeAsync(batch, cancellationToken)
            .ConfigureAwait(false);

        Logger.LogInformation("{Job} tick finished for {Count} player(s).", Name, batch.Count);
    }
}
