using TradingIntel.Domain.Common;

namespace TradingIntel.Domain.Models;

public sealed record SbcChallenge
{
    public SbcChallenge(Guid id, string title, string setName, DateTime observedAtUtc, IEnumerable<SbcRequirement> requirements)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("id cannot be empty.", nameof(id));
        }

        Id = id;
        Title = Guard.NotNullOrWhiteSpace(title, nameof(title));
        SetName = Guard.NotNullOrWhiteSpace(setName, nameof(setName));
        ObservedAtUtc = Guard.Utc(observedAtUtc, nameof(observedAtUtc));

        var materialized = requirements?.ToArray() ?? throw new ArgumentNullException(nameof(requirements));
        if (materialized.Length == 0)
        {
            throw new ArgumentException("At least one SBC requirement is required.", nameof(requirements));
        }

        Requirements = materialized;
    }

    public Guid Id { get; }

    public string Title { get; }

    public string SetName { get; }

    public DateTime ObservedAtUtc { get; }

    public IReadOnlyList<SbcRequirement> Requirements { get; }
}