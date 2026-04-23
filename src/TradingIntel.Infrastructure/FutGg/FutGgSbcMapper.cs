using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
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

    public SbcChallenge Map(FutGgSbcListingItemParsed parsed, DateTime observedAtUtc)
    {
        try
        {
            var id = DeterministicGuid(parsed.DetailsUrl);
            var repeatability = parsed.RepeatableUnlimited
                ? SbcRepeatability.Unlimited()
                : parsed.RepeatableCount is null
                    ? SbcRepeatability.Unknown()
                    : SbcRepeatability.Limited(parsed.RepeatableCount.Value);

            var requirements = MapRequirements(parsed.RequirementLines);

            return new SbcChallenge(
                id: id,
                title: parsed.Title,
                category: parsed.Category,
                expiresAtUtc: parsed.ExpiresAtUtc,
                repeatability: repeatability,
                setName: "futgg",
                observedAtUtc: observedAtUtc,
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

    private static IReadOnlyList<SbcRequirement> MapRequirements(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
        {
            return new[] { new SbcRequirement("visible_requirements", 0) };
        }

        var requirements = new List<SbcRequirement>(lines.Count);
        foreach (var raw in lines)
        {
            var key = BuildKey(raw);
            var minimum = ExtractFirstInteger(raw) ?? 1;
            requirements.Add(new SbcRequirement(key, minimum));
        }

        return requirements;
    }

    private static int? ExtractFirstInteger(string text)
    {
        var match = Regex.Match(text, @"\d+");
        return match.Success ? int.Parse(match.Value) : null;
    }

    private static string BuildKey(string text)
    {
        var normalized = text.ToLowerInvariant();
        var chars = normalized
            .Select(c => char.IsLetterOrDigit(c) ? c : '_')
            .ToArray();

        var collapsed = Regex.Replace(new string(chars), "_{2,}", "_").Trim('_');
        return string.IsNullOrWhiteSpace(collapsed) ? "visible_requirement" : collapsed;
    }
}

