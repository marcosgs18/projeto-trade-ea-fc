namespace TradingIntel.Domain.ValueObjects;

/// <summary>
/// Origin of a <see cref="TradingIntel.Domain.Models.TrackedPlayer"/> entry.
/// Persisted as the integer column <c>source</c> on <c>tracked_players</c>;
/// values must stay stable because they are referenced by historical rows.
/// </summary>
public enum WatchlistSource
{
    /// <summary>
    /// Imported from the bundled JSON catalog seed
    /// (<c>data/players-catalog.seed.json</c>) at host startup.
    /// </summary>
    Seed = 0,

    /// <summary>
    /// Imported from the legacy <c>Jobs:PriceCollection:Players</c> /
    /// <c>Jobs:OpportunityRecompute:Players</c> arrays in <c>appsettings</c>.
    /// Kept for backward compatibility — new entries should go through
    /// <see cref="Api"/>.
    /// </summary>
    AppSettings = 1,

    /// <summary>
    /// Added at runtime through <c>POST /api/watchlist</c>.
    /// </summary>
    Api = 2,
}
