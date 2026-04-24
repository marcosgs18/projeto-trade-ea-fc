using Microsoft.EntityFrameworkCore;
using TradingIntel.Application.Persistence;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;
using TradingIntel.Infrastructure.Persistence.Entities;

namespace TradingIntel.Infrastructure.Persistence.Repositories;

public sealed class WatchlistRepository : IWatchlistRepository
{
    private readonly TradingIntelDbContext _dbContext;

    public WatchlistRepository(TradingIntelDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<TrackedPlayer>> GetActiveAsync(CancellationToken cancellationToken)
    {
        var rows = await _dbContext.TrackedPlayers
            .AsNoTracking()
            .Where(e => e.IsActive)
            .OrderBy(e => e.PlayerId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(ToDomain).ToList();
    }

    public async Task<TrackedPlayer?> GetByPlayerIdAsync(long playerId, CancellationToken cancellationToken)
    {
        var record = await _dbContext.TrackedPlayers
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.PlayerId == playerId, cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : ToDomain(record);
    }

    public async Task<TrackedPlayer> UpsertAsync(TrackedPlayer player, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(player);

        var playerId = player.Player.PlayerId;
        var existing = await _dbContext.TrackedPlayers
            .FirstOrDefaultAsync(e => e.PlayerId == playerId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            var inserted = new TrackedPlayerRecord
            {
                PlayerId = playerId,
                DisplayName = player.Player.DisplayName,
                Overall = player.Overall,
                Source = (int)player.Source,
                AddedAtUtc = player.AddedAtUtc,
                LastCollectedAtUtc = player.LastCollectedAtUtc,
                IsActive = player.IsActive,
            };
            _dbContext.TrackedPlayers.Add(inserted);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return ToDomain(inserted);
        }

        existing.DisplayName = player.Player.DisplayName;
        existing.Overall = player.Overall;
        existing.IsActive = player.IsActive;
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDomain(existing);
    }

    public async Task<bool> DeactivateAsync(long playerId, CancellationToken cancellationToken)
    {
        var rows = await _dbContext.TrackedPlayers
            .Where(e => e.PlayerId == playerId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(e => e.IsActive, false),
                cancellationToken)
            .ConfigureAwait(false);

        return rows > 0;
    }

    public async Task TouchLastCollectedAsync(
        IReadOnlyCollection<long> playerIds,
        DateTime collectedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(playerIds);
        if (collectedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("collectedAtUtc must be UTC.", nameof(collectedAtUtc));
        }

        if (playerIds.Count == 0)
        {
            return;
        }

        var idList = playerIds.ToList();
        await _dbContext.TrackedPlayers
            .Where(e => idList.Contains(e.PlayerId))
            .ExecuteUpdateAsync(
                s => s.SetProperty(e => e.LastCollectedAtUtc, collectedAtUtc),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<TrackedPlayer> Items, int TotalCount)> QueryPagedAsync(
        WatchlistQuery query,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var q = _dbContext.TrackedPlayers.AsNoTracking();

        if (!query.IncludeInactive)
        {
            q = q.Where(e => e.IsActive);
        }

        if (query.Source is { } source)
        {
            var sourceInt = (int)source;
            q = q.Where(e => e.Source == sourceInt);
        }

        if (query.MinOverall is { } minOverall)
        {
            q = q.Where(e => e.Overall != null && e.Overall >= minOverall);
        }

        q = q.OrderBy(e => e.PlayerId);

        var totalCount = await q.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await q.Skip(skip).Take(take).ToListAsync(cancellationToken).ConfigureAwait(false);

        return (rows.Select(ToDomain).ToList(), totalCount);
    }

    private static TrackedPlayer ToDomain(TrackedPlayerRecord record)
    {
        var addedAt = DateTime.SpecifyKind(record.AddedAtUtc, DateTimeKind.Utc);
        var lastCollected = record.LastCollectedAtUtc is null
            ? (DateTime?)null
            : DateTime.SpecifyKind(record.LastCollectedAtUtc.Value, DateTimeKind.Utc);

        return new TrackedPlayer(
            new PlayerReference(record.PlayerId, record.DisplayName),
            record.Overall,
            (WatchlistSource)record.Source,
            addedAt,
            lastCollected,
            record.IsActive);
    }
}
