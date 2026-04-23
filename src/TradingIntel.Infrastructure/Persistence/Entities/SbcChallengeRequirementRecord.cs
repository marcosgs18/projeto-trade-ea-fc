namespace TradingIntel.Infrastructure.Persistence.Entities;

internal sealed class SbcChallengeRequirementRecord
{
    public Guid Id { get; set; }

    public Guid ChallengeId { get; set; }

    public string Key { get; set; } = string.Empty;

    public int Minimum { get; set; }

    public int? Maximum { get; set; }

    public int Order { get; set; }

    public SbcChallengeRecord Challenge { get; set; } = null!;
}
