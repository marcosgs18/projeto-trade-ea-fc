using Microsoft.EntityFrameworkCore;
using TradingIntel.Application.Persistence;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;
using TradingIntel.Infrastructure.Persistence.Entities;

namespace TradingIntel.Infrastructure.Persistence.Repositories;

public sealed class PlayerPriceSnapshotRepository : IPlayerPriceSnapshotRepository
{
    private readonly TradingIntelDbContext _dbContext;

    public PlayerPriceSnapshotRepository(TradingIntelDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddRangeAsync(IEnumerable<PlayerPriceSnapshot> snapshots, CancellationToken cancellationToken)
    {
        var records = snapshots.Select(Map).ToList();
        if (records.Count == 0)
        {
            return;
        }

        _dbContext.PlayerPriceSnapshots.AddRange(records);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PlayerPriceSnapshot>> GetByPlayerAsync(
        long playerId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var records = await _dbContext.PlayerPriceSnapshots
            .AsNoTracking()
            .Where(r => r.PlayerId == playerId && r.CapturedAtUtc >= fromUtc && r.CapturedAtUtc <= toUtc)
            .OrderBy(r => r.CapturedAtUtc)
            .ToListAsync(cancellationToken);

        return records.Select(ToDomain).ToList();
    }

    public async Task<PlayerPriceSnapshot?> GetLatestForPlayerAsync(
        long playerId,
        string source,
        CancellationToken cancellationToken)
    {
        var record = await _dbContext.PlayerPriceSnapshots
            .AsNoTracking()
            .Where(r => r.PlayerId == playerId && r.Source == source)
            .OrderByDescending(r => r.CapturedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return record is null ? null : ToDomain(record);
    }

    public async Task<PlayerPriceSnapshot?> GetLatestFutbinPriceForPlayerAsync(
        long playerId,
        CancellationToken cancellationToken)
    {
        var record = await _dbContext.PlayerPriceSnapshots
            .AsNoTracking()
            .Where(r => r.PlayerId == playerId && r.Source.StartsWith("futbin:"))
            .OrderByDescending(r => r.CapturedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return record is null ? null : ToDomain(record);
    }

    public async Task<IReadOnlyList<PlayerPriceSnapshot>> GetFutbinPriceHistoryAsync(
        long playerId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var records = await _dbContext.PlayerPriceSnapshots
            .AsNoTracking()
            .Where(r =>
                r.PlayerId == playerId
                && r.Source.StartsWith("futbin:")
                && r.CapturedAtUtc >= fromUtc
                && r.CapturedAtUtc <= toUtc)
            .OrderBy(r => r.CapturedAtUtc)
            .ToListAsync(cancellationToken);

        return records.Select(ToDomain).ToList();
    }

    public async Task<(IReadOnlyList<PlayerPriceSnapshot> Items, int TotalCount)> GetByPlayerPagedAsync(
        long playerId,
        string? source,
        DateTime fromUtc,
        DateTime toUtc,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.PlayerPriceSnapshots
            .AsNoTracking()
            .Where(r => r.PlayerId == playerId && r.CapturedAtUtc >= fromUtc && r.CapturedAtUtc <= toUtc);

        if (!string.IsNullOrWhiteSpace(source))
        {
            query = query.Where(r => r.Source == source);
        }

        query = query.OrderBy(r => r.CapturedAtUtc);

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var records = await query
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (records.Select(ToDomain).ToList(), totalCount);
    }

    private static PlayerPriceSnapshotRecord Map(PlayerPriceSnapshot snapshot) => new()
    {
        Id = Guid.NewGuid(),
        PlayerId = snapshot.Player.PlayerId,
        PlayerDisplayName = snapshot.Player.DisplayName,
        Source = snapshot.Source,
        CapturedAtUtc = snapshot.CapturedAtUtc,
        BuyNowPrice = snapshot.BuyNowPrice.Value,
        SellNowPrice = snapshot.SellNowPrice?.Value,
        MedianMarketPrice = snapshot.MedianMarketPrice.Value,
    };

    private static PlayerPriceSnapshot ToDomain(PlayerPriceSnapshotRecord record)
    {
        var player = new PlayerReference(record.PlayerId, record.PlayerDisplayName);
        return new PlayerPriceSnapshot(
            player,
            record.Source,
            DateTime.SpecifyKind(record.CapturedAtUtc, DateTimeKind.Utc),
            new Coins(record.BuyNowPrice),
            record.SellNowPrice is null ? null : new Coins(record.SellNowPrice.Value),
            new Coins(record.MedianMarketPrice));
    }
}
