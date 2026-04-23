using TradingIntel.Domain.Common;

namespace TradingIntel.Domain.ValueObjects;

public readonly record struct RatingBand
{
    public RatingBand(int fromRating, int toRating)
    {
        if (fromRating is < 0 or > 99)
        {
            throw new ArgumentOutOfRangeException(nameof(fromRating), "fromRating must be between 0 and 99.");
        }

        if (toRating is < 0 or > 99)
        {
            throw new ArgumentOutOfRangeException(nameof(toRating), "toRating must be between 0 and 99.");
        }

        if (toRating < fromRating)
        {
            throw new ArgumentException("toRating must be greater than or equal to fromRating.", nameof(toRating));
        }

        FromRating = fromRating;
        ToRating = toRating;
    }

    public int FromRating { get; }

    public int ToRating { get; }

    public override string ToString() => $"{FromRating}-{ToRating}";
}