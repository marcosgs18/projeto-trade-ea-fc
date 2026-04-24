using TradingIntel.Domain.ValueObjects;

namespace TradingIntel.Application.Watchlist;

/// <summary>
/// Single entry passed to <see cref="IWatchlistSeedService"/> on host startup.
/// The seed pipeline normalizes the input across all configured sources
/// (catalog JSON + legacy <c>appsettings</c> arrays) before upserting
/// <c>tracked_players</c> rows.
/// </summary>
public sealed record WatchlistSeedEntry(
    long PlayerId,
    string DisplayName,
    int? Overall,
    WatchlistSource Source);
