namespace TradingIntel.Infrastructure.Persistence.Entities;

internal sealed class TrackedPlayerRecord
{
    public long PlayerId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public int? Overall { get; set; }

    public int Source { get; set; }

    public DateTime AddedAtUtc { get; set; }

    public DateTime? LastCollectedAtUtc { get; set; }

    public bool IsActive { get; set; }
}
