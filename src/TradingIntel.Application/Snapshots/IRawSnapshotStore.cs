using TradingIntel.Domain.Models;

namespace TradingIntel.Application.Snapshots;

public interface IRawSnapshotStore
{
    Task SaveAsync(SourceSnapshotMetadata metadata, string rawPayload, CancellationToken cancellationToken);
}

