using TradingIntel.Domain.Common;
using TradingIntel.Domain.ValueObjects;

namespace TradingIntel.Domain.Models;

public sealed record OpportunityReason
{
    public OpportunityReason(string code, string message, decimal weight)
    {
        Code = Guard.NotNullOrWhiteSpace(code, nameof(code));
        Message = Guard.NotNullOrWhiteSpace(message, nameof(message));

        if (weight is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), "weight must be between 0 and 1.");
        }

        Weight = weight;
    }

    public string Code { get; }

    public string Message { get; }

    public decimal Weight { get; }
}