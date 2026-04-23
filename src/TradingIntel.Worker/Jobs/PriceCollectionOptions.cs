namespace TradingIntel.Worker.Jobs;

public sealed class PriceCollectionOptions : JobScheduleOptions
{
    /// <summary>
    /// Static watchlist of players to poll each tick. Kept explicit in config for
    /// V1 so the job is deterministic and testable before we have a catalogue
    /// ingestion flow.
    /// </summary>
    public IList<PlayerWatchlistEntry> Players { get; set; } = new List<PlayerWatchlistEntry>();
}

public sealed class PlayerWatchlistEntry
{
    public long PlayerId { get; set; }

    public string? Name { get; set; }
}
