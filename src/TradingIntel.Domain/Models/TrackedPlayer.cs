using TradingIntel.Domain.Common;
using TradingIntel.Domain.ValueObjects;

namespace TradingIntel.Domain.Models;

/// <summary>
/// Player explicitly tracked by the watchlist. Replaces the static
/// <c>Jobs:PriceCollection:Players</c> array in <c>appsettings</c> as the
/// source of truth for which players the price collection job and the
/// opportunity recompute should consider.
/// </summary>
/// <remarks>
/// The watchlist lives in persistence (single table <c>tracked_players</c>)
/// and is populated by three independent inputs (see <see cref="WatchlistSource"/>):
/// a versioned JSON seed, the legacy <c>appsettings</c> import (idempotent on
/// startup), and explicit operator additions through the API. The aggregate
/// is intentionally small because the read path is hot (every price tick).
/// </remarks>
public sealed record TrackedPlayer
{
    public TrackedPlayer(
        PlayerReference player,
        int? overall,
        WatchlistSource source,
        DateTime addedAtUtc,
        DateTime? lastCollectedAtUtc,
        bool isActive)
    {
        Player = player;

        if (overall is < 0 or > 99)
        {
            throw new ArgumentOutOfRangeException(nameof(overall), "overall must be between 0 and 99 when provided.");
        }

        Overall = overall;
        Source = source;
        AddedAtUtc = Guard.Utc(addedAtUtc, nameof(addedAtUtc));

        if (lastCollectedAtUtc is { } collected)
        {
            LastCollectedAtUtc = Guard.Utc(collected, nameof(lastCollectedAtUtc));
        }

        IsActive = isActive;
    }

    public PlayerReference Player { get; }

    /// <summary>
    /// Optional EA overall rating (0-99). Useful for diagnostics and for
    /// catalog-driven seeding ("top N by rating") later. Not used by the
    /// scoring pipeline.
    /// </summary>
    public int? Overall { get; }

    public WatchlistSource Source { get; }

    public DateTime AddedAtUtc { get; }

    /// <summary>
    /// Last time the price collection job successfully fetched a snapshot
    /// for this player. <c>null</c> until the first successful tick.
    /// </summary>
    public DateTime? LastCollectedAtUtc { get; }

    /// <summary>
    /// Soft-delete flag. Inactive entries stay in the table to preserve
    /// history (e.g. who added it and when) but are skipped by collection
    /// and recompute reads.
    /// </summary>
    public bool IsActive { get; }
}
