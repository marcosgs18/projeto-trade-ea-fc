namespace TradingIntel.Application.Persistence;

public interface IRawSnapshotRepository
{
    Task<IReadOnlyList<StoredRawSnapshot>> GetBySourceAsync(
        string source,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken);

    Task<StoredRawSnapshot?> GetLatestAsync(string source, CancellationToken cancellationToken);
}
