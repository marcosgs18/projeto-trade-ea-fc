using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingIntel.Application.JobHealth;
using TradingIntel.Application.Persistence;
using TradingIntel.Application.PlayerMarket;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;

namespace TradingIntel.Worker.Jobs;

/// <summary>
/// Iterates the configured player watchlist and, for each player, fetches the
/// market snapshot via <see cref="IPlayerMarketClient"/> (which writes the raw
/// payload itself before returning) and persists the normalized
/// <c>PlayerPriceSnapshot</c> and <c>MarketListingSnapshot</c> collections.
/// A failure for a single player is logged and skipped so one bad player does
/// not abort the whole tick or starve the scheduler. The concrete client is
/// selected by <c>Market:Source</c> (see <c>docs/source-futgg-market.md</c>).
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
        var watchlist = serviceProvider.GetRequiredService<IWatchlistRepository>();
        var players = await watchlist.GetActiveAsync(cancellationToken).ConfigureAwait(false);

        if (players.Count == 0)
        {
            Logger.LogWarning(
                "{Job} watchlist is empty (tracked_players has no active rows). " +
                "Add entries through POST /api/watchlist or the data/players-catalog.seed.json file.",
                Name);
            return;
        }

        var client = serviceProvider.GetRequiredService<IPlayerMarketClient>();
        var priceRepo = serviceProvider.GetRequiredService<IPlayerPriceSnapshotRepository>();
        var listingRepo = serviceProvider.GetRequiredService<IMarketListingSnapshotRepository>();

        int collectedPlayers = 0;
        int totalPriceSnapshots = 0;
        int totalListingSnapshots = 0;
        int perPlayerFailures = 0;
        var collectedIds = new List<long>(players.Count);

        foreach (var entry in players)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var playerRef = entry.Player;

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
                collectedIds.Add(playerRef.PlayerId);

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

        if (collectedIds.Count > 0)
        {
            await watchlist
                .TouchLastCollectedAsync(collectedIds, DateTime.UtcNow, cancellationToken)
                .ConfigureAwait(false);
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
