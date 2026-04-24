using TradingIntel.Application.Persistence;
using TradingIntel.Domain.Models;

namespace TradingIntel.Tests.Worker.Fakes;

/// <summary>
/// Minimal in-memory <see cref="IWatchlistRepository"/> for worker / api unit
/// tests. Not thread-safe and intentionally tiny — SQLite-backed behaviour is
/// covered by <c>WatchlistRepositoryTests</c>.
/// </summary>
internal sealed class InMemoryWatchlistRepository : IWatchlistRepository
{
    private readonly Dictionary<long, TrackedPlayer> _byId = new();
    public List<long> LastTouchedIds { get; } = new();
    public DateTime? LastTouchedAtUtc { get; private set; }

    public void Add(TrackedPlayer player) => _byId[player.Player.PlayerId] = player;

    public Task<IReadOnlyList<TrackedPlayer>> GetActiveAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<TrackedPlayer> result = _byId.Values
            .Where(p => p.IsActive)
            .OrderBy(p => p.Player.PlayerId)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<TrackedPlayer?> GetByPlayerIdAsync(long playerId, CancellationToken cancellationToken)
    {
        _byId.TryGetValue(playerId, out var existing);
        return Task.FromResult(existing);
    }

    public Task<TrackedPlayer> UpsertAsync(TrackedPlayer player, CancellationToken cancellationToken)
    {
        _byId[player.Player.PlayerId] = player;
        return Task.FromResult(player);
    }

    public Task<bool> DeactivateAsync(long playerId, CancellationToken cancellationToken)
    {
        if (!_byId.TryGetValue(playerId, out var existing))
        {
            return Task.FromResult(false);
        }

        _byId[playerId] = new TrackedPlayer(
            existing.Player,
            existing.Overall,
            existing.Source,
            existing.AddedAtUtc,
            existing.LastCollectedAtUtc,
            isActive: false);
        return Task.FromResult(true);
    }

    public Task TouchLastCollectedAsync(
        IReadOnlyCollection<long> playerIds,
        DateTime collectedAtUtc,
        CancellationToken cancellationToken)
    {
        LastTouchedIds.Clear();
        LastTouchedIds.AddRange(playerIds);
        LastTouchedAtUtc = collectedAtUtc;
        foreach (var id in playerIds)
        {
            if (_byId.TryGetValue(id, out var existing))
            {
                _byId[id] = new TrackedPlayer(
                    existing.Player,
                    existing.Overall,
                    existing.Source,
                    existing.AddedAtUtc,
                    collectedAtUtc,
                    existing.IsActive);
            }
        }
        return Task.CompletedTask;
    }

    public Task<(IReadOnlyList<TrackedPlayer> Items, int TotalCount)> QueryPagedAsync(
        WatchlistQuery query,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var filtered = _byId.Values.AsEnumerable();
        if (!query.IncludeInactive)
        {
            filtered = filtered.Where(p => p.IsActive);
        }
        if (query.Source is { } s)
        {
            filtered = filtered.Where(p => p.Source == s);
        }
        if (query.MinOverall is { } m)
        {
            filtered = filtered.Where(p => p.Overall is { } o && o >= m);
        }

        var ordered = filtered.OrderBy(p => p.Player.PlayerId).ToList();
        var page = ordered.Skip(skip).Take(take).ToList();
        return Task.FromResult(((IReadOnlyList<TrackedPlayer>)page, ordered.Count));
    }
}
