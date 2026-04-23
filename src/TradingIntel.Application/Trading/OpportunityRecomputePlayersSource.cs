namespace TradingIntel.Application.Trading;

public sealed class OpportunityRecomputeWatchlistRow
{
    public long PlayerId { get; set; }

    public string? Name { get; set; }

    public int? Overall { get; set; }
}

/// <summary>
/// Lista de jogadores em <c>Jobs:OpportunityRecompute:Players</c> (bind no API; Worker usa <see cref="OpportunityRecomputeOptions"/>).
/// </summary>
public sealed class OpportunityRecomputePlayersSource
{
    public IList<OpportunityRecomputeWatchlistRow> Players { get; set; } = new List<OpportunityRecomputeWatchlistRow>();
}
