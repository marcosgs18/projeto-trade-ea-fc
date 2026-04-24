namespace TradingIntel.Application.Watchlist;

/// <summary>
/// Idempotent boot-time bridge between configuration / on-disk seeds and the
/// persisted watchlist (<c>tracked_players</c>). Hosts call <see cref="SeedAsync"/>
/// once per startup, after migrations have been applied.
/// </summary>
public interface IWatchlistSeedService
{
    Task<WatchlistSeedReport> SeedAsync(
        IEnumerable<WatchlistSeedEntry> appSettingsEntries,
        CancellationToken cancellationToken);
}
