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

    /// <summary>
    /// Optional overall rating of the player in the roster. Kept as config-only
    /// metadata in this slice: downstream tasks (rating-band scoring, SBC
    /// matching) will cross this with <c>ISbcChallengeRepository</c> requirements
    /// to answer "which active SBCs does this card help fulfill?".
    /// </summary>
    public int? Overall { get; set; }
}
