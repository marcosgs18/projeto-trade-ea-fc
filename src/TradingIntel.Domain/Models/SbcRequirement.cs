using TradingIntel.Domain.Common;

namespace TradingIntel.Domain.Models;

public sealed record SbcRequirement
{
    public SbcRequirement(string key, int minimum, int? maximum = null)
    {
        Key = Guard.NotNullOrWhiteSpace(key, nameof(key));

        if (minimum < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimum), "minimum cannot be negative.");
        }

        if (maximum is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum), "maximum cannot be negative.");
        }

        if (maximum is not null && maximum < minimum)
        {
            throw new ArgumentException("maximum must be greater than or equal to minimum.", nameof(maximum));
        }

        Minimum = minimum;
        Maximum = maximum;
    }

    public string Key { get; }

    public int Minimum { get; }

    public int? Maximum { get; }
}