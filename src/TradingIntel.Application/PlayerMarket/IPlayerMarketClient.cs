using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;

namespace TradingIntel.Application.PlayerMarket;

/// <summary>
/// Transport-neutral contract used by the price collection job to fetch the
/// current market state for a player. Implementations exist per source
/// (FUT.GG, FUTBIN, ...); the concrete one is chosen by <c>Market:Source</c>
/// in configuration.
/// </summary>
public interface IPlayerMarketClient
{
    Task<PlayerMarketSnapshot> GetPlayerMarketSnapshotAsync(PlayerReference player, CancellationToken cancellationToken);
}

/// <summary>
/// Aggregate of normalized data produced by a single market fetch for one
/// player. The <see cref="RawPayload"/> is already persisted by the client
/// through <c>IRawSnapshotStore</c> before this snapshot is returned — see the
/// "raw-before-normalized" invariant in <c>docs/worker.md</c>.
/// </summary>
public sealed record PlayerMarketSnapshot(
    string Source,
    DateTime CapturedAtUtc,
    string CorrelationId,
    string RawPayload,
    IReadOnlyList<PlayerPriceSnapshot> PriceSnapshots,
    IReadOnlyList<MarketListingSnapshot> LowestListingSnapshots);
