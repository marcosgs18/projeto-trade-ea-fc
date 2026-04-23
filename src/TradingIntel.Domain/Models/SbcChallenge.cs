using TradingIntel.Domain.Common;
using TradingIntel.Domain.ValueObjects;

namespace TradingIntel.Domain.Models;

public sealed record SbcChallenge
{
    public SbcChallenge(
        Guid id,
        string title,
        string category,
        DateTime? expiresAtUtc,
        SbcRepeatability repeatability,
        string setName,
        DateTime observedAtUtc,
        IEnumerable<SbcRequirement> requirements)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("id cannot be empty.", nameof(id));
        }

        Id = id;
        Title = Guard.NotNullOrWhiteSpace(title, nameof(title));
        Category = Guard.NotNullOrWhiteSpace(category, nameof(category));
        ExpiresAtUtc = expiresAtUtc is null ? null : Guard.Utc(expiresAtUtc.Value, nameof(expiresAtUtc));
        Repeatability = repeatability ?? throw new ArgumentNullException(nameof(repeatability));
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

    public string Category { get; }

    public DateTime? ExpiresAtUtc { get; }

    public SbcRepeatability Repeatability { get; }

    public string SetName { get; }

    public DateTime ObservedAtUtc { get; }

    public IReadOnlyList<SbcRequirement> Requirements { get; }
}