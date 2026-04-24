using TradingIntel.Api.Contracts;
using TradingIntel.Domain.Models;

namespace TradingIntel.Api.Mapping;

internal static class SbcMapper
{
    public static SbcChallengeResponse ToResponse(SbcChallenge c)
    {
        var reqs = c.Requirements
            .Select(r => new SbcRequirementResponse(r.Key, r.Minimum, r.Maximum))
            .ToList();

        return new SbcChallengeResponse(
            c.Id,
            c.Title,
            c.Category,
            c.ExpiresAtUtc,
            c.Repeatability.Kind.ToString(),
            c.Repeatability.MaxCompletions,
            c.SetName,
            c.ObservedAtUtc,
            reqs);
    }
}
