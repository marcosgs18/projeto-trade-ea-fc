using TradingIntel.Domain.Models;

namespace TradingIntel.Application.Persistence;

/// <summary>
/// Port for persisting and querying normalized SBC challenges. Historical
/// snapshots of the raw FUT.GG payload remain in <see cref="IRawSnapshotStore"/>
/// / <see cref="IRawSnapshotRepository"/>; this repository stores only the
/// current canonical state (upsert by <see cref="SbcChallenge.Id"/>), which is
/// enough for "SBCs ativos agora" + rating-band scoring.
/// </summary>
public interface ISbcChallengeRepository
{
    /// <summary>
    /// Persists a batch of challenges, replacing previous rows with the same
    /// <see cref="SbcChallenge.Id"/>. Requirements are fully replaced for each
    /// challenge on every call (they are a value list, not a standalone
    /// aggregate). Empty input is a no-op.
    /// </summary>
    Task UpsertRangeAsync(IEnumerable<SbcChallenge> challenges, CancellationToken cancellationToken);

    Task<SbcChallenge?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Composable query. All filters are optional and combined with AND.
    /// Results are ordered by <see cref="SbcChallenge.ExpiresAtUtc"/> ascending
    /// (nulls last) and then by title, so callers get a stable enumeration.
    /// </summary>
    Task<IReadOnlyList<SbcChallenge>> QueryAsync(
        SbcChallengeQuery query,
        CancellationToken cancellationToken);

    /// <summary>
    /// Lista paginada para a API de SBCs ativos/expirados com filtros compostos.
    /// </summary>
    Task<(IReadOnlyList<SbcChallenge> Items, int TotalCount)> QueryActivePagedAsync(
        SbcActiveListQuery query,
        int skip,
        int take,
        CancellationToken cancellationToken);
}
