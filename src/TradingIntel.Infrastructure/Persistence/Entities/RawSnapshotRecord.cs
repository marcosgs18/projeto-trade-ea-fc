namespace TradingIntel.Infrastructure.Persistence.Entities;

internal sealed class RawSnapshotRecord
{
    public Guid Id { get; set; }

    public string Source { get; set; } = string.Empty;

    public DateTime CapturedAtUtc { get; set; }

    public int RecordCount { get; set; }

    public string CorrelationId { get; set; } = string.Empty;

    public string PayloadHash { get; set; } = string.Empty;

    public string RawPayload { get; set; } = string.Empty;
}
