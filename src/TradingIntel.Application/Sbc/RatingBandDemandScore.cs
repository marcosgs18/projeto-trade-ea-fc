using TradingIntel.Domain.ValueObjects;

namespace TradingIntel.Application.Sbc;

/// <summary>
/// Score agregado de demanda para uma faixa de rating, com motivos legíveis que o explicam.
/// </summary>
public sealed record RatingBandDemandScore
{
    public RatingBandDemandScore(
        RatingBand band,
        double score,
        int totalRequiredCards,
        IReadOnlyList<Guid> contributingChallengeIds,
        IReadOnlyList<DemandReason> reasons)
    {
        if (double.IsNaN(score) || score < 0 || score > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "score must be between 0 and 1.");
        }

        if (totalRequiredCards < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalRequiredCards), "totalRequiredCards cannot be negative.");
        }

        Band = band;
        Score = score;
        TotalRequiredCards = totalRequiredCards;
        ContributingChallengeIds = contributingChallengeIds
            ?? throw new ArgumentNullException(nameof(contributingChallengeIds));
        Reasons = reasons ?? throw new ArgumentNullException(nameof(reasons));
    }

    public RatingBand Band { get; }

    /// <summary>Score normalizado em [0, 1]. 1,0 indica demanda saturada.</summary>
    public double Score { get; }

    /// <summary>Soma de cartas requisitadas pelos SBCs ativos nesta faixa.</summary>
    public int TotalRequiredCards { get; }

    public IReadOnlyList<Guid> ContributingChallengeIds { get; }

    /// <summary>Motivos ordenados por relevância decrescente.</summary>
    public IReadOnlyList<DemandReason> Reasons { get; }
}
