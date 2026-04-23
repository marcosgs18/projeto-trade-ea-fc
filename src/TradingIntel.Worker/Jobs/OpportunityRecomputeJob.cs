using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingIntel.Application.Trading;
using TradingIntel.Application.JobHealth;

namespace TradingIntel.Worker.Jobs;

/// <summary>
/// Recomputa oportunidades de trade para a watchlist configurada e aplica TTL de obsolescência nas linhas persistidas.
/// </summary>
public sealed class OpportunityRecomputeJob : ScheduledJob
{
    public const string Name = "opportunity-recompute";

    private readonly OpportunityRecomputeOptions _options;

    public OpportunityRecomputeJob(
        IOptions<OpportunityRecomputeOptions> options,
        IServiceScopeFactory scopeFactory,
        IJobHealthRegistry health,
        ILogger<OpportunityRecomputeJob> logger)
        : base(Name, options.Value, scopeFactory, health, logger)
    {
        _options = options.Value;
    }

    protected override async Task ExecuteTickAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var players = _options.Players;
        if (players is null || players.Count == 0)
        {
            Logger.LogWarning("{Job} has no players configured; nothing to recompute.", Name);
            return;
        }

        var recompute = serviceProvider.GetRequiredService<IOpportunityRecomputeService>();

        var batch = players
            .Where(p => p.PlayerId > 0)
            .Select(p =>
            {
                var displayName = string.IsNullOrWhiteSpace(p.Name)
                    ? $"player-{p.PlayerId}"
                    : p.Name!.Trim();
                return new OpportunityRecomputePlayer(p.PlayerId, displayName, p.Overall);
            })
            .ToList();

        if (batch.Count == 0)
        {
            Logger.LogWarning("{Job} has no valid watchlist entries.", Name);
            return;
        }

        await recompute
            .RecomputeAsync(batch, cancellationToken)
            .ConfigureAwait(false);

        Logger.LogInformation("{Job} tick finished for {Count} player(s).", Name, batch.Count);
    }
}
