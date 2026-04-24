namespace TradingIntel.Application.Watchlist;

/// <summary>
/// Outcome of one <see cref="IWatchlistSeedService.SeedAsync"/> run, surfaced
/// in the host startup logs and consumed by tests.
/// </summary>
/// <param name="CatalogEntriesRead">Total entries deduplicated from the JSON
/// seed file (after schema validation).</param>
/// <param name="AppSettingsEntriesRead">Total entries deduplicated from the
/// legacy <c>appsettings</c> arrays.</param>
/// <param name="Inserted">Entries that did not exist in <c>tracked_players</c>
/// and were inserted as new rows.</param>
/// <param name="Updated">Entries that already existed and had display name /
/// overall refreshed (source &amp; <c>AddedAtUtc</c> preserved).</param>
/// <param name="Skipped">Entries skipped because they failed validation
/// (<c>playerId &lt;= 0</c>, blank display name, etc.).</param>
public sealed record WatchlistSeedReport(
    int CatalogEntriesRead,
    int AppSettingsEntriesRead,
    int Inserted,
    int Updated,
    int Skipped);
