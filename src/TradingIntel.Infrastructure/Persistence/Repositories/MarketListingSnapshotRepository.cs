using Microsoft.EntityFrameworkCore;
using TradingIntel.Application.Persistence;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;
using TradingIntel.Infrastructure.Persistence.Entities;

namespace TradingIntel.Infrastructure.Persistence.Repositories;

public sealed class MarketListingSnapshotRepository : IMarketListingSnapshotRepository
{
    private readonly TradingIntelDbContext _dbContext;

    public MarketListingSnapshotRepository(TradingIntelDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddRangeAsync(IEnumerable<MarketListingSnapshot> snapshots, CancellationToken cancellationToken)
    {
        var records = snapshots.Select(Map).ToList();
        if (records.Count == 0)
        {
            return;
        }

        _dbContext.MarketListingSnapshots.AddRange(records);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MarketListingSnapshot>> GetByPlayerAsync(
        long playerId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var records = await _dbContext.MarketListingSnapshots
            .AsNoTracking()
            .Where(r => r.PlayerId == playerId && r.CapturedAtUtc >= fromUtc && r.CapturedAtUtc <= toUtc)
            .OrderBy(r => r.CapturedAtUtc)
            .ToListAsync(cancellationToken);

        return records.Select(ToDomain).ToList();
    }

    public async Task<MarketListingSnapshot?> GetByListingIdAsync(string listingId, CancellationToken cancellationToken)
    {
        var record = await _dbContext.MarketListingSnapshots
            .AsNoTracking()
            .Where(r => r.ListingId == listingId)
            .OrderByDescending(r => r.CapturedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return record is null ? null : ToDomain(record);
    }

    public async Task<IReadOnlyList<MarketListingSnapshot>> GetFutbinListingsByPlayerAsync(
        long playerId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var records = await _dbContext.MarketListingSnapshots
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

    public async Task<(IReadOnlyList<MarketListingSnapshot> Items, int TotalCount)> GetByPlayerPagedAsync(
        long playerId,
        DateTime fromUtc,
        DateTime toUtc,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.MarketListingSnapshots
            .AsNoTracking()
            .Where(r => r.PlayerId == playerId && r.CapturedAtUtc >= fromUtc && r.CapturedAtUtc <= toUtc)
            .OrderBy(r => r.CapturedAtUtc);

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var records = await query
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (records.Select(ToDomain).ToList(), totalCount);
    }

    private static MarketListingSnapshotRecord Map(MarketListingSnapshot snapshot) => new()
    {
        Id = Guid.NewGuid(),
        ListingId = snapshot.ListingId,
        PlayerId = snapshot.Player.PlayerId,
        PlayerDisplayName = snapshot.Player.DisplayName,
        Source = snapshot.Source,
        CapturedAtUtc = snapshot.CapturedAtUtc,
        ExpiresAtUtc = snapshot.ExpiresAtUtc,
        StartingBid = snapshot.StartingBid.Value,
        BuyNowPrice = snapshot.BuyNowPrice.Value,
    };

    private static MarketListingSnapshot ToDomain(MarketListingSnapshotRecord record)
    {
        var player = new PlayerReference(record.PlayerId, record.PlayerDisplayName);
        return new MarketListingSnapshot(
            record.ListingId,
            player,
            record.Source,
            DateTime.SpecifyKind(record.CapturedAtUtc, DateTimeKind.Utc),
            new Coins(record.StartingBid),
            new Coins(record.BuyNowPrice),
            DateTime.SpecifyKind(record.ExpiresAtUtc, DateTimeKind.Utc));
    }
}
