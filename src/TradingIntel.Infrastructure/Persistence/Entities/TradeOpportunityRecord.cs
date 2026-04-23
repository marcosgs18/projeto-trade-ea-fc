namespace TradingIntel.Infrastructure.Persistence.Entities;

internal sealed class TradeOpportunityRecord
{
    public long PlayerId { get; set; }

    public Guid OpportunityId { get; set; }

    public string PlayerDisplayName { get; set; } = string.Empty;

    public DateTime DetectedAtUtc { get; set; }

    public decimal ExpectedBuyPrice { get; set; }

    public decimal ExpectedSellPrice { get; set; }

    public decimal Confidence { get; set; }

    public string ReasonsJson { get; set; } = string.Empty;

    public string SuggestionsJson { get; set; } = string.Empty;

    public DateTime LastRecomputedAtUtc { get; set; }

    public bool IsStale { get; set; }
}
