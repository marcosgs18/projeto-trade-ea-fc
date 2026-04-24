using TradingIntel.Application.Trading;

namespace TradingIntel.Worker.Jobs;

public sealed class OpportunityRecomputeOptions : JobScheduleOptions
{
    public IList<OpportunityRecomputeWatchlistRow> Players { get; set; } =
        new List<OpportunityRecomputeWatchlistRow>();

    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromMinutes(15);
}
