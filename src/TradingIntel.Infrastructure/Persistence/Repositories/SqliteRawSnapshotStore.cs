using Microsoft.EntityFrameworkCore;
using TradingIntel.Application.Persistence;
using TradingIntel.Application.Snapshots;
using TradingIntel.Domain.Models;
using TradingIntel.Infrastructure.Persistence.Entities;

namespace TradingIntel.Infrastructure.Persistence.Repositories;

public sealed class SqliteRawSnapshotStore : IRawSnapshotStore, IRawSnapshotRepository
{
    private readonly TradingIntelDbContext _dbContext;

    public SqliteRawSnapshotStore(TradingIntelDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task SaveAsync(SourceSnapshotMetadata metadata, string rawPayload, CancellationToken cancellationToken)
    {
        var record = new RawSnapshotRecord
        {
            Id = Guid.NewGuid(),
            Source = metadata.Source,
            CapturedAtUtc = metadata.CapturedAtUtc,
            RecordCount = metadata.RecordCount,
            CorrelationId = metadata.CorrelationId,
            PayloadHash = metadata.PayloadHash,
            RawPayload = rawPayload,
        };

        _dbContext.RawSnapshots.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StoredRawSnapshot>> GetBySourceAsync(
        string source,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var records = await _dbContext.RawSnapshots
            .AsNoTracking()
            .Where(r => r.Source == source && r.CapturedAtUtc >= fromUtc && r.CapturedAtUtc <= toUtc)
            .OrderBy(r => r.CapturedAtUtc)
            .ToListAsync(cancellationToken);

        return records.Select(Map).ToList();
    }

    public async Task<StoredRawSnapshot?> GetLatestAsync(string source, CancellationToken cancellationToken)
    {
        var record = await _dbContext.RawSnapshots
            .AsNoTracking()
            .Where(r => r.Source == source)
            .OrderByDescending(r => r.CapturedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return record is null ? null : Map(record);
    }

    private static StoredRawSnapshot Map(RawSnapshotRecord record)
    {
        var metadata = new SourceSnapshotMetadata(
            record.Source,
            DateTime.SpecifyKind(record.CapturedAtUtc, DateTimeKind.Utc),
            record.RecordCount,
            record.CorrelationId,
            record.PayloadHash);

        return new StoredRawSnapshot(record.Id, metadata, record.RawPayload);
    }
}
