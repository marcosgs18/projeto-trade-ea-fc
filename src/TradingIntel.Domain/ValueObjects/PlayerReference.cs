using TradingIntel.Domain.Common;

namespace TradingIntel.Domain.ValueObjects;

public readonly record struct PlayerReference
{
    public PlayerReference(long playerId, string displayName)
    {
        if (playerId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(playerId), "playerId must be greater than zero.");
        }

        PlayerId = playerId;
        DisplayName = Guard.NotNullOrWhiteSpace(displayName, nameof(displayName));
    }

    public long PlayerId { get; }

    public string DisplayName { get; }

    public override string ToString() => $"{DisplayName} ({PlayerId})";
}