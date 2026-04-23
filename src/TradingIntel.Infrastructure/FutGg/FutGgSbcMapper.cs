using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;

namespace TradingIntel.Infrastructure.FutGg;

public sealed class FutGgSbcMapper
{
    private readonly ILogger _logger;

    public FutGgSbcMapper(ILogger logger)
    {
        _logger = logger;
    }

    public SbcChallenge Map(FutGgSbcListingItemParsed parsed)
    {
        try
        {
            var id = DeterministicGuid(parsed.DetailsUrl);
            var repeatability = parsed.RepeatableUnlimited
                ? SbcRepeatability.Unlimited()
                : parsed.RepeatableCount is null
                    ? SbcRepeatability.Unknown()
                    : SbcRepeatability.Limited(parsed.RepeatableCount.Value);

            // We don't have visible requirements from listing in this data source; keep a sentinel requirement.
            var requirements = new[]
            {
                new SbcRequirement("visible_requirements", 0)
            };

            return new SbcChallenge(
                id: id,
                title: parsed.Title,
                category: parsed.Category,
                expiresAtUtc: parsed.ExpiresAtUtc,
                repeatability: repeatability,
                setName: "futgg",
                observedAtUtc: DateTime.UtcNow,
                requirements: requirements);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to map FUT.GG SBC listing item. title={Title} url={Url}", parsed.Title, parsed.DetailsUrl);
            throw;
        }
    }

    private static Guid DeterministicGuid(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);

        Span<byte> guidBytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(guidBytes);

        // RFC 4122 version 5-ish (name-based) formatting
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | (5 << 4));
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);

        return new Guid(guidBytes);
    }
}

