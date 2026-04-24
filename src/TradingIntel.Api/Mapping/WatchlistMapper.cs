using TradingIntel.Api.Contracts;
using TradingIntel.Domain.Models;

namespace TradingIntel.Api.Mapping;

internal static class WatchlistMapper
{
    public static TrackedPlayerResponse ToResponse(TrackedPlayer player) => new(
        PlayerId: player.Player.PlayerId,
        DisplayName: player.Player.DisplayName,
        Overall: player.Overall,
        Source: player.Source.ToString(),
        AddedAtUtc: player.AddedAtUtc,
        LastCollectedAtUtc: player.LastCollectedAtUtc,
        IsActive: player.IsActive);
}
