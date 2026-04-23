using TradingIntel.Domain.Common;

namespace TradingIntel.Domain.ValueObjects;

public readonly record struct Coins
{
    public Coins(decimal value)
    {
        Value = Guard.NonNegative(value, nameof(value));
    }

    public decimal Value { get; }

    public static implicit operator decimal(Coins coins) => coins.Value;

    public static Coins operator +(Coins left, Coins right) => new(left.Value + right.Value);

    public static Coins operator -(Coins left, Coins right)
    {
        var result = left.Value - right.Value;
        return new Coins(result < 0 ? 0 : result);
    }

    public override string ToString() => $"{Value:0} coins";
}