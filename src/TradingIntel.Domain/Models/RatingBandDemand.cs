using TradingIntel.Domain.Common;
using TradingIntel.Domain.ValueObjects;

namespace TradingIntel.Domain.Models;

public sealed record RatingBandDemand
{
    public RatingBandDemand(Guid challengeId, RatingBand band, int requiredCards)
    {
        if (challengeId == Guid.Empty)
        {
            throw new ArgumentException("challengeId cannot be empty.", nameof(challengeId));
        }

        ChallengeId = challengeId;
        Band = band;
        RequiredCards = Guard.Positive(requiredCards, nameof(requiredCards));
    }

    public Guid ChallengeId { get; }

    public RatingBand Band { get; }

    public int RequiredCards { get; }
}