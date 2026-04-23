using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;

namespace TradingIntel.Application.Sbc;

/// <summary>
/// Implementação padrão de <see cref="IRatingBandDemandService"/>.
/// </summary>
/// <remarks>
/// A lógica é pura (sem I/O) e determinística: dada a mesma lista de <see cref="SbcChallenge"/> e o mesmo
/// <c>nowUtc</c>, o resultado é idêntico. Isso facilita testes e auditoria.
/// </remarks>
public sealed class RatingBandDemandService : IRatingBandDemandService
{
    private const int MinRatingConsidered = 60;
    private const int MaxRatingConsidered = 99;
    private const int BandWidth = 2; // [rating, rating+BandWidth]

    private static readonly Regex IntegerRegex = new(@"\d+", RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, double> CategoryWeights =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["upgrades"] = 1.0,
            ["players"] = 0.85,
            ["icons"] = 0.9,
            ["heroes"] = 0.85,
            ["challenges"] = 0.7,
            ["foundations"] = 0.65,
        };

    private const double DefaultCategoryWeight = 0.6;

    private readonly ILogger<RatingBandDemandService> _logger;

    public RatingBandDemandService(ILogger<RatingBandDemandService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IReadOnlyList<RatingBandDemandScore> ComputeDemand(
        IReadOnlyList<SbcChallenge> challenges,
        DateTime nowUtc,
        RatingBandDemandWeights? weights = null)
    {
        if (challenges is null) throw new ArgumentNullException(nameof(challenges));
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("nowUtc must be UTC.", nameof(nowUtc));
        }

        var w = weights ?? RatingBandDemandWeights.Default;

        if (challenges.Count == 0)
        {
            _logger.LogInformation("ComputeDemand: no challenges provided, returning empty demand.");
            return Array.Empty<RatingBandDemandScore>();
        }

        // band key -> accumulator
        var perBand = new Dictionary<RatingBand, BandAccumulator>();
        var skippedChallenges = 0;
        var skippedRequirements = 0;

        foreach (var challenge in challenges)
        {
            var urgency = ComputeUrgencyFactor(challenge.ExpiresAtUtc, nowUtc);
            if (urgency <= 0)
            {
                skippedChallenges++;
                _logger.LogDebug(
                    "Skipping expired/past SBC. challengeId={ChallengeId} title={Title} expires={Expires}",
                    challenge.Id, challenge.Title, challenge.ExpiresAtUtc);
                continue;
            }

            var repeatability = ComputeRepeatabilityFactor(challenge.Repeatability);
            var category = ResolveCategoryFactor(challenge.Category);

            foreach (var requirement in challenge.Requirements)
            {
                var target = TryInterpretRating(requirement);
                if (target is null)
                {
                    skippedRequirements++;
                    continue;
                }

                var band = ToBand(target.Value.Rating);
                if (!perBand.TryGetValue(band, out var accumulator))
                {
                    accumulator = new BandAccumulator(band);
                    perBand[band] = accumulator;
                }

                accumulator.Add(new ChallengeContribution(
                    Challenge: challenge,
                    RequiredCards: target.Value.RequiredCards,
                    IsSquadRating: target.Value.IsSquadRating,
                    TargetRating: target.Value.Rating,
                    Repeatability: repeatability,
                    Urgency: urgency,
                    Category: category));
            }
        }

        if (skippedChallenges > 0 || skippedRequirements > 0)
        {
            _logger.LogInformation(
                "ComputeDemand summary: challenges={Total} skippedChallenges={SkippedChallenges} skippedRequirements={SkippedRequirements} bands={BandCount}",
                challenges.Count, skippedChallenges, skippedRequirements, perBand.Count);
        }

        var results = perBand.Values
            .Select(acc => acc.Build(w, nowUtc))
            .Where(r => r.Score > 0)
            .OrderByDescending(r => r.Score)
            .ThenByDescending(r => r.TotalRequiredCards)
            .ThenBy(r => r.Band.FromRating)
            .ToArray();

        return results;
    }

    // ---------- factor helpers ----------

    private static double ComputeRepeatabilityFactor(SbcRepeatability repeatability)
    {
        return repeatability.Kind switch
        {
            SbcRepeatabilityKind.Unlimited => 1.0,
            SbcRepeatabilityKind.Limited => ClampUnit(
                (repeatability.MaxCompletions ?? 1) / 5.0),
            SbcRepeatabilityKind.NotRepeatable => 0.35,
            SbcRepeatabilityKind.Unknown => 0.5,
            _ => 0.5,
        };
    }

    private static double ComputeUrgencyFactor(DateTime? expiresAtUtc, DateTime nowUtc)
    {
        if (expiresAtUtc is null)
        {
            return 0.5;
        }

        var delta = expiresAtUtc.Value - nowUtc;
        if (delta <= TimeSpan.Zero)
        {
            return 0.0;
        }

        if (delta <= TimeSpan.FromHours(24))
        {
            return 1.0;
        }

        if (delta <= TimeSpan.FromHours(72))
        {
            return 0.75;
        }

        if (delta <= TimeSpan.FromDays(7))
        {
            return 0.6;
        }

        return 0.45;
    }

    private static double ResolveCategoryFactor(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return DefaultCategoryWeight;
        }

        return CategoryWeights.TryGetValue(category.Trim(), out var value)
            ? value
            : DefaultCategoryWeight;
    }

    // ---------- requirement interpretation ----------

    internal static RatingTarget? TryInterpretRating(SbcRequirement requirement)
    {
        var key = requirement.Key ?? string.Empty;

        var isSquadRating =
            key.Contains("team_rating", StringComparison.OrdinalIgnoreCase)
            || key.Contains("squad_rating", StringComparison.OrdinalIgnoreCase);

        var hasRatingSignal = isSquadRating
            || key.Contains("overall", StringComparison.OrdinalIgnoreCase)
            || key.Contains("rating", StringComparison.OrdinalIgnoreCase)
            || key.Contains("rated", StringComparison.OrdinalIgnoreCase);

        if (!hasRatingSignal)
        {
            return null;
        }

        var ints = IntegerRegex.Matches(key)
            .Select(m => int.Parse(m.Value, CultureInfo.InvariantCulture))
            .ToArray();

        int? rating = null;
        foreach (var n in ints)
        {
            if (n is >= MinRatingConsidered and <= MaxRatingConsidered)
            {
                rating = rating is null ? n : Math.Max(rating.Value, n);
            }
        }

        if (rating is null
            && requirement.Minimum is >= MinRatingConsidered and <= MaxRatingConsidered)
        {
            rating = requirement.Minimum;
        }

        if (rating is null)
        {
            return null;
        }

        int? playerCount = null;
        foreach (var n in ints)
        {
            if (n is >= 1 and <= 23 && n != rating.Value)
            {
                playerCount = playerCount is null ? n : Math.Max(playerCount.Value, n);
            }
        }

        // Fallback: team/squad rating aplica-se à squad inteira — assumimos 1 carta adicional "marginal"
        // puxada para a faixa. Para "exactly N players: X+ overall", usamos N.
        var requiredCards = playerCount ?? 1;

        return new RatingTarget(rating.Value, requiredCards, isSquadRating);
    }

    private static RatingBand ToBand(int rating)
    {
        var from = Math.Clamp(rating, 0, 99);
        var to = Math.Clamp(rating + BandWidth, from, 99);
        return new RatingBand(from, to);
    }

    private static double ClampUnit(double value)
    {
        if (double.IsNaN(value)) return 0;
        if (value < 0) return 0;
        if (value > 1) return 1;
        return value;
    }

    // ---------- internals ----------

    internal readonly record struct RatingTarget(int Rating, int RequiredCards, bool IsSquadRating);

    private sealed record ChallengeContribution(
        SbcChallenge Challenge,
        int RequiredCards,
        bool IsSquadRating,
        int TargetRating,
        double Repeatability,
        double Urgency,
        double Category);

    private sealed class BandAccumulator
    {
        private readonly List<ChallengeContribution> _contributions = new();

        public BandAccumulator(RatingBand band)
        {
            Band = band;
        }

        public RatingBand Band { get; }

        public void Add(ChallengeContribution contribution) => _contributions.Add(contribution);

        public RatingBandDemandScore Build(RatingBandDemandWeights weights, DateTime nowUtc)
        {
            var totalCards = _contributions.Sum(c => c.RequiredCards);
            var uniqueChallengeIds = _contributions
                .Select(c => c.Challenge.Id)
                .Distinct()
                .ToArray();

            double rawScore = 0;
            var perChallengeContribution = new Dictionary<Guid, double>();

            foreach (var contribution in _contributions)
            {
                var volumeFactor = Math.Min(1.0, (double)contribution.RequiredCards / weights.CardVolumeSaturation);

                var contribScore =
                    weights.CardVolume * volumeFactor
                    + weights.Repeatability * contribution.Repeatability
                    + weights.ExpiryUrgency * contribution.Urgency
                    + weights.Category * contribution.Category;

                rawScore += contribScore;

                if (!perChallengeContribution.TryAdd(contribution.Challenge.Id, contribScore))
                {
                    perChallengeContribution[contribution.Challenge.Id] += contribScore;
                }
            }

            var score = ClampUnit(rawScore / weights.ChallengeSaturation);

            var reasons = BuildReasons(weights, perChallengeContribution, totalCards, nowUtc);

            return new RatingBandDemandScore(
                band: Band,
                score: score,
                totalRequiredCards: totalCards,
                contributingChallengeIds: uniqueChallengeIds,
                reasons: reasons);
        }

        private IReadOnlyList<DemandReason> BuildReasons(
            RatingBandDemandWeights weights,
            IReadOnlyDictionary<Guid, double> perChallengeContribution,
            int totalCards,
            DateTime nowUtc)
        {
            var ordered = _contributions
                .GroupBy(c => c.Challenge.Id)
                .Select(g => new
                {
                    Contribution = g.First(),
                    Cards = g.Sum(x => x.RequiredCards),
                    Score = perChallengeContribution[g.Key],
                })
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Cards)
                .Take(weights.MaxReasonsPerBand)
                .ToArray();

            var reasons = new List<DemandReason>(ordered.Length + 1);

            // Summary reason (volume of cards / unique challenges).
            var uniqueChallenges = _contributions.Select(c => c.Challenge.Id).Distinct().Count();
            var summaryWeight = ClampUnit((double)totalCards / Math.Max(1, weights.CardVolumeSaturation));
            reasons.Add(new DemandReason(
                code: "AGGREGATED_DEMAND",
                message: $"{uniqueChallenges} SBC(s) ativos demandam {totalCards} carta(s) na faixa {Band}.",
                weight: summaryWeight));

            foreach (var item in ordered)
            {
                var contrib = item.Contribution;
                var message = BuildChallengeMessage(contrib, item.Cards, nowUtc);
                var weight = ClampUnit(item.Score / Math.Max(1e-9, weights.ChallengeSaturation));

                reasons.Add(new DemandReason(
                    code: BuildChallengeReasonCode(contrib),
                    message: message,
                    weight: weight));
            }

            return reasons;
        }

        private static string BuildChallengeMessage(ChallengeContribution contrib, int cards, DateTime nowUtc)
        {
            var repeatDescription = contrib.Challenge.Repeatability.Kind switch
            {
                SbcRepeatabilityKind.Unlimited => "repetível ilimitado",
                SbcRepeatabilityKind.Limited =>
                    $"repetível {contrib.Challenge.Repeatability.MaxCompletions}x",
                SbcRepeatabilityKind.NotRepeatable => "uso único",
                _ => "repetibilidade desconhecida",
            };

            var expiryDescription = contrib.Challenge.ExpiresAtUtc is null
                ? "sem expiração informada"
                : $"expira em {FormatRelativeExpiry(contrib.Challenge.ExpiresAtUtc.Value - nowUtc)}";

            var ratingScope = contrib.IsSquadRating
                ? $"team rating {contrib.TargetRating}+"
                : $"{contrib.TargetRating}+ overall";

            return $"'{contrib.Challenge.Title}' ({contrib.Challenge.Category}) pede {cards} carta(s) {ratingScope}; {repeatDescription}, {expiryDescription}.";
        }

        private static string BuildChallengeReasonCode(ChallengeContribution contrib)
        {
            var repeatCode = contrib.Challenge.Repeatability.Kind switch
            {
                SbcRepeatabilityKind.Unlimited => "REPEATABLE_UNLIMITED",
                SbcRepeatabilityKind.Limited => "REPEATABLE_LIMITED",
                SbcRepeatabilityKind.NotRepeatable => "ONE_SHOT",
                _ => "REPEATABILITY_UNKNOWN",
            };
            return $"CHALLENGE_{repeatCode}";
        }

        private static string FormatRelativeExpiry(TimeSpan delta)
        {
            if (delta.TotalHours < 1) return $"{Math.Max(1, (int)delta.TotalMinutes)}min";
            if (delta.TotalHours < 48) return $"{(int)delta.TotalHours}h";
            return $"{(int)delta.TotalDays}d";
        }

        private static double ClampUnit(double value)
        {
            if (double.IsNaN(value)) return 0;
            if (value < 0) return 0;
            if (value > 1) return 1;
            return value;
        }
    }
}
