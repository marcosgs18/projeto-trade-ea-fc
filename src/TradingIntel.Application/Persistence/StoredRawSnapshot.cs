using TradingIntel.Domain.Models;

namespace TradingIntel.Application.Persistence;

public sealed record StoredRawSnapshot(
    Guid Id,
    SourceSnapshotMetadata Metadata,
    string RawPayload);
