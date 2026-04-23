namespace TradingIntel.Application.Persistence;

/// <summary>
/// Optional filters for <see cref="ISbcChallengeRepository.QueryAsync"/>. Each
/// property is treated as an independent filter and they are combined with AND;
/// <c>null</c> means "don't filter on this".
/// </summary>
public sealed record SbcChallengeQuery
{
    /// <summary>
    /// When set, only challenges that are active as of this UTC instant are
    /// returned. A challenge counts as active when it has no expiration
    /// (<see cref="Domain.Models.SbcChallenge.ExpiresAtUtc"/> is null) or the
    /// expiration is still in the future.
    /// </summary>
    public DateTime? ActiveAsOfUtc { get; init; }

    /// <summary>
    /// Case-insensitive substring match against
    /// <see cref="Domain.Models.SbcChallenge.Category"/>.
    /// </summary>
    public string? CategoryContains { get; init; }

    /// <summary>
    /// When set, returns only challenges that contain at least one team-rating
    /// requirement (keys <c>min_team_rating</c> or <c>squad_rating</c>,
    /// case-insensitive) whose <c>Minimum</c> is less than or equal to this
    /// overall, i.e. challenges that a player of this rating helps fulfill.
    /// </summary>
    public int? MatchesOverall { get; init; }

    /// <summary>
    /// Keys (case-insensitive) that are treated as "team rating" requirements
    /// when filtering by <see cref="MatchesOverall"/>. Kept as a public constant
    /// so downstream services can align with the same convention.
    /// </summary>
    public static IReadOnlyList<string> TeamRatingRequirementKeys { get; } = new[]
    {
        "min_team_rating",
        "squad_rating",
    };
}
