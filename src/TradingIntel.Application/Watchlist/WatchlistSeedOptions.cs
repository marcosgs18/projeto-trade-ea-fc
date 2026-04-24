namespace TradingIntel.Application.Watchlist;

/// <summary>
/// Bound to the <c>Watchlist</c> configuration section.
/// </summary>
public sealed class WatchlistSeedOptions
{
    public const string SectionName = "Watchlist";

    /// <summary>
    /// Path to the catalog seed JSON. Resolved relative to the host
    /// content root when not absolute. Defaults to
    /// <c>data/players-catalog.seed.json</c>.
    /// </summary>
    public string CatalogSeedPath { get; set; } = "data/players-catalog.seed.json";

    /// <summary>
    /// When <c>true</c> the seed file is required and a missing or invalid
    /// file fails the host startup. When <c>false</c> (default) the seed
    /// runs in best-effort mode: a missing file logs a warning and the
    /// service still imports the legacy <c>appsettings</c> arrays.
    /// </summary>
    public bool RequireCatalogSeed { get; set; }
}
