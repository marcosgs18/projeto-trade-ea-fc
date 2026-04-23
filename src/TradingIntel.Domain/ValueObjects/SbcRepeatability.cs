using TradingIntel.Domain.Common;

namespace TradingIntel.Domain.ValueObjects;

public enum SbcRepeatabilityKind
{
    Unknown = 0,
    NotRepeatable = 1,
    Limited = 2,
    Unlimited = 3
}

public sealed record SbcRepeatability
{
    private SbcRepeatability(SbcRepeatabilityKind kind, int? maxCompletions)
    {
        Kind = kind;
        MaxCompletions = maxCompletions;
    }

    public SbcRepeatabilityKind Kind { get; }

    public int? MaxCompletions { get; }

    public static SbcRepeatability Unknown() => new(SbcRepeatabilityKind.Unknown, null);

    public static SbcRepeatability NotRepeatable() => new(SbcRepeatabilityKind.NotRepeatable, 1);

    public static SbcRepeatability Limited(int maxCompletions)
    {
        return new(SbcRepeatabilityKind.Limited, Guard.Positive(maxCompletions, nameof(maxCompletions)));
    }

    public static SbcRepeatability Unlimited() => new(SbcRepeatabilityKind.Unlimited, null);
}

