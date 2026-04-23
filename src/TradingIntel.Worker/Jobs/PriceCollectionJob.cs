using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingIntel.Application.Futbin;
using TradingIntel.Application.Persistence;
using TradingIntel.Domain.ValueObjects;
using TradingIntel.Worker.Health;

namespace TradingIntel.Worker.Jobs;

/// <summary>
/// Iterates the configured player watchlist and, for each player, fetches the
/// Futbin market snapshot via <see cref="IFutbinMarketClient"/> (which writes
/// the raw payload itself before returning) and persists the normalized
/// <c>PlayerPriceSnapshot</c> and <c>MarketListingSnapshot</c> collections.
/// A failure for a single player is logged and skipped so one bad player does
/// not abort the whole tick or starve the scheduler.
/// </summary>
public sealed class PriceCollectionJob : ScheduledJob
{
    public const string Name = "price-collection";

    private readonly PriceCollectionOptions _options;

    public PriceCollectionJob(
        IOptions<PriceCollectionOptions> options,
        IServiceScopeFactory scopeFactory,
        IJobHealthRegistry health,
        ILogger<PriceCollectionJob> logger)
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
            Logger.LogWarning("{Job} has no players configured; nothing to collect.", Name);
            return;
        }

        var client = serviceProvider.GetRequiredService<IFutbinMarketClient>();
        var priceRepo = serviceProvider.GetRequiredService<IPlayerPriceSnapshotRepository>();
        var listingRepo = serviceProvider.GetRequiredService<IMarketListingSnapshotRepository>();

        int collectedPlayers = 0;
        int totalPriceSnapshots = 0;
        int totalListingSnapshots = 0;
        int perPlayerFailures = 0;

        foreach (var entry in players)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.PlayerId <= 0)
            {
                Logger.LogWarning(
                    "{Job} skipping invalid watchlist entry. playerId={PlayerId}",
                    Name,
                    entry.PlayerId);
                continue;
            }

            var displayName = string.IsNullOrWhiteSpace(entry.Name)
                ? $"player-{entry.PlayerId}"
                : entry.Name!;
            var playerRef = new PlayerReference(entry.PlayerId, displayName);

            try
            {
                var snapshot = await client
                    .GetPlayerMarketSnapshotAsync(playerRef, cancellationToken)
                    .ConfigureAwait(false);

                if (snapshot.PriceSnapshots.Count > 0)
                {
                    await priceRepo
                        .AddRangeAsync(snapshot.PriceSnapshots, cancellationToken)
                        .ConfigureAwait(false);
                }

                if (snapshot.LowestListingSnapshots.Count > 0)
                {
                    await listingRepo
                        .AddRangeAsync(snapshot.LowestListingSnapshots, cancellationToken)
                        .ConfigureAwait(false);
                }

                collectedPlayers++;
                totalPriceSnapshots += snapshot.PriceSnapshots.Count;
                totalListingSnapshots += snapshot.LowestListingSnapshots.Count;

                Logger.LogDebug(
                    "{Job} collected player {PlayerId} ({PlayerName}). prices={PriceCount} listings={ListingCount} correlationId={CorrelationId}",
                    Name,
                    playerRef.PlayerId,
                    playerRef.DisplayName,
                    snapshot.PriceSnapshots.Count,
                    snapshot.LowestListingSnapshots.Count,
                    snapshot.CorrelationId);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                perPlayerFailures++;
                Logger.LogWarning(
                    ex,
                    "{Job} failed to collect player {PlayerId} ({PlayerName}); continuing with the rest of the watchlist.",
                    Name,
                    playerRef.PlayerId,
                    playerRef.DisplayName);
            }
        }

        Logger.LogInformation(
            "{Job} tick summary. collectedPlayers={CollectedPlayers}/{TotalPlayers} prices={PriceCount} listings={ListingCount} perPlayerFailures={PerPlayerFailures}",
            Name,
            collectedPlayers,
            players.Count,
            totalPriceSnapshots,
            totalListingSnapshots,
            perPlayerFailures);

        if (collectedPlayers == 0 && perPlayerFailures > 0)
        {
            throw new InvalidOperationException(
                $"All {perPlayerFailures} player(s) in the watchlist failed to collect.");
        }
    }
}
