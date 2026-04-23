using TradingIntel.Domain.Common;
using TradingIntel.Domain.ValueObjects;

namespace TradingIntel.Domain.Models;

public sealed record TradeOpportunity
{
    public TradeOpportunity(
        Guid id,
        PlayerReference player,
        DateTime detectedAtUtc,
        Coins expectedBuyPrice,
        Coins expectedSellPrice,
        ConfidenceScore confidence,
        IEnumerable<OpportunityReason> reasons,
        IEnumerable<ExecutionSuggestion> suggestions)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("id cannot be empty.", nameof(id));
        }

        Id = id;
        Player = player;
        DetectedAtUtc = Guard.Utc(detectedAtUtc, nameof(detectedAtUtc));

        if (expectedSellPrice.Value <= expectedBuyPrice.Value)
        {
            throw new ArgumentException("expectedSellPrice must be greater than expectedBuyPrice.", nameof(expectedSellPrice));
        }

        ExpectedBuyPrice = expectedBuyPrice;
        ExpectedSellPrice = expectedSellPrice;
        ExpectedProfit = new Coins(expectedSellPrice.Value - expectedBuyPrice.Value);
        Confidence = confidence;

        var reasonItems = reasons?.ToArray() ?? throw new ArgumentNullException(nameof(reasons));
        if (reasonItems.Length == 0)
        {
            throw new ArgumentException("At least one reason is required.", nameof(reasons));
        }

        var suggestionItems = suggestions?.ToArray() ?? throw new ArgumentNullException(nameof(suggestions));
        if (suggestionItems.Length == 0)
        {
            throw new ArgumentException("At least one execution suggestion is required.", nameof(suggestions));
        }

        Reasons = reasonItems;
        Suggestions = suggestionItems;
    }

    public Guid Id { get; }

    public PlayerReference Player { get; }

    public DateTime DetectedAtUtc { get; }

    public Coins ExpectedBuyPrice { get; }

    public Coins ExpectedSellPrice { get; }

    public Coins ExpectedProfit { get; }

    public ConfidenceScore Confidence { get; }

    public IReadOnlyList<OpportunityReason> Reasons { get; }

    public IReadOnlyList<ExecutionSuggestion> Suggestions { get; }
}