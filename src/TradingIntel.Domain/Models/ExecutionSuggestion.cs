using TradingIntel.Domain.Common;
using TradingIntel.Domain.ValueObjects;

namespace TradingIntel.Domain.Models;

public enum ExecutionAction
{
    Buy = 1,
    ListForSale = 2,
    Hold = 3
}

public sealed record ExecutionSuggestion
{
    public ExecutionSuggestion(
        Guid id,
        Guid opportunityId,
        ExecutionAction action,
        Coins targetPrice,
        DateTime validUntilUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("id cannot be empty.", nameof(id));
        }

        if (opportunityId == Guid.Empty)
        {
            throw new ArgumentException("opportunityId cannot be empty.", nameof(opportunityId));
        }

        if (!Enum.IsDefined(action))
        {
            throw new ArgumentOutOfRangeException(nameof(action), "action must be a known value.");
        }

        Id = id;
        OpportunityId = opportunityId;
        Action = action;
        TargetPrice = targetPrice;
        ValidUntilUtc = Guard.Utc(validUntilUtc, nameof(validUntilUtc));
    }

    public Guid Id { get; }

    public Guid OpportunityId { get; }

    public ExecutionAction Action { get; }

    public Coins TargetPrice { get; }

    public DateTime ValidUntilUtc { get; }
}