using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;

namespace TradingIntel.Application.Futbin;

public interface IFutbinMarketClient
{
    Task<FutbinPlayerMarketSnapshot> GetPlayerMarketSnapshotAsync(PlayerReference player, CancellationToken cancellationToken);
}

public sealed record FutbinPlayerMarketSnapshot(
    string Source,
    DateTime CapturedAtUtc,
    string CorrelationId,
    string RawPayload,
    IReadOnlyList<PlayerPriceSnapshot> PriceSnapshots,
    IReadOnlyList<MarketListingSnapshot> LowestListingSnapshots);
