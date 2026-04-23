namespace TradingIntel.Domain.ValueObjects;

public readonly record struct ConfidenceScore
{
    public ConfidenceScore(decimal value)
    {
        if (value is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Confidence score must be between 0 and 1.");
        }

        Value = value;
    }

    public decimal Value { get; }

    public override string ToString() => $"{Value:P0}";
}