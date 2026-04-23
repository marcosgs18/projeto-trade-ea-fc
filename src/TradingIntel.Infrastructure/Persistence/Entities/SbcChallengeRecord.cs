using TradingIntel.Domain.ValueObjects;

namespace TradingIntel.Infrastructure.Persistence.Entities;

internal sealed class SbcChallengeRecord
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public DateTime? ExpiresAtUtc { get; set; }

    public SbcRepeatabilityKind RepeatabilityKind { get; set; }

    public int? RepeatabilityMaxCompletions { get; set; }

    public string SetName { get; set; } = string.Empty;

    public DateTime ObservedAtUtc { get; set; }

    public List<SbcChallengeRequirementRecord> Requirements { get; set; } = new();
}
