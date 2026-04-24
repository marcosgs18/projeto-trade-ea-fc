using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace TradingIntel.Infrastructure.FutGg;

public sealed record FutGgSbcListingItemParsed(
    string Title,
    string Category,
    string DetailsUrl,
    DateTime? ExpiresAtUtc,
    int? RepeatableCount,
    bool RepeatableUnlimited,
    IReadOnlyList<string> RequirementLines);

public sealed class FutGgSbcListingParser
{
    // r.jina.ai embeds a small "FC Coin" icon inside the header link, sometimes
    // preceded by a formatted coin amount (thousand-separated), e.g.
    //   `### [Ederson 355,950![Image 18: FC Coin](...)](url)`  (older shape)
    //   `### [Ederson ![Image 11: FC Coin](...)](url)`         (current shape)
    //   `### [TOTS Challenge 2](url)`                          (no icon at all)
    // Stripping the optional coin amount and inline markdown image together lets
    // us match a plain `### [title](url)` header with a simple, greedy-safe regex
    // regardless of which variant FUT.GG is currently rendering.
    private static readonly Regex CoinAmountAndImageRegex = new(
        @"(?:\s*\d{1,3}(?:,\d{3})+)?\s*!\[[^\]]*\]\([^)]*\)",
        RegexOptions.Compiled);

    private static readonly Regex HeaderRegex = new(
        @"^\s*###\s*\[(?<title>[^\]]+?)\s*\]\((?<url>https:\/\/www\.fut\.gg\/sbc\/(?<category>[^\/]+)\/[^)]+\/)\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ExpiresRegex = new(
        @"Expires\s+in\s+(?<value>\d+)\s+(?<unit>hours?|days?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // FUT.GG renders both `Repeatable 2` (with whitespace) and `Repeatable-`,
    // `Repeatable∞` (no whitespace). Use \s* so both shapes are captured.
    private static readonly Regex RepeatableRegex = new(
        @"Repeatable\s*(?<count>\d+|∞|-)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly ILogger _logger;

    public FutGgSbcListingParser(ILogger logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<FutGgSbcListingItemParsed> ParseListing(string renderedListingText, DateTime capturedAtUtc)
    {
        if (renderedListingText is null) throw new ArgumentNullException(nameof(renderedListingText));

        var lines = renderedListingText.Split('\n');
        var items = new List<FutGgSbcListingItemParsed>();

        // The jina.ai rendered version emits entries like:
        //   ### [Ederson ![Image 11: FC Coin](.../coin.webp)](https://www.fut.gg/sbc/players/26-830-ederson/)
        // and, for entries without the coin icon:
        //   ### [TOTS Challenge 2](https://www.fut.gg/sbc/challenges/26-813-tots-challenge-2/)
        // followed by metadata lines: [Challenges 7]...[Expires in 20 days]...[Repeatable-]

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith("### [", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var sanitized = CoinAmountAndImageRegex.Replace(line, string.Empty);
            var headerMatch = HeaderRegex.Match(sanitized);

            if (!headerMatch.Success)
            {
                _logger.LogWarning(
                    "Failed to parse FUT.GG SBC header line. lineIndex={LineIndex} line={Line}",
                    i,
                    line);
                continue;
            }

            var title = headerMatch.Groups["title"].Value.Trim();
            var detailsUrl = headerMatch.Groups["url"].Value.Trim();
            var category = headerMatch.Groups["category"].Value.Trim();

            DateTime? expiresAtUtc = null;
            int? repeatableCount = null;
            bool repeatableUnlimited = false;

            // Walk a small window ahead for metadata, but stop at the next `### [`
            // header so we never inherit expiry/repeatable values from the
            // following SBC entry.
            for (var j = i + 1; j < Math.Min(i + 12, lines.Length); j++)
            {
                var meta = lines[j];
                if (meta.TrimStart().StartsWith("### [", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (expiresAtUtc is null)
                {
                    var expires = ExpiresRegex.Match(meta);
                    if (expires.Success)
                    {
                        expiresAtUtc = TryComputeExpiry(capturedAtUtc, expires.Groups["value"].Value, expires.Groups["unit"].Value);
                    }
                }

                if (repeatableCount is null && !repeatableUnlimited)
                {
                    var repeat = RepeatableRegex.Match(meta);
                    if (repeat.Success)
                    {
                        var raw = repeat.Groups["count"].Value;
                        if (raw == "∞")
                        {
                            repeatableUnlimited = true;
                            repeatableCount = null;
                        }
                        else if (raw == "-")
                        {
                            repeatableCount = null;
                            repeatableUnlimited = false;
                        }
                        else if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedCount))
                        {
                            repeatableCount = parsedCount;
                            repeatableUnlimited = false;
                        }
                    }
                }
            }

            // Requirements are not visible on listing in the rendered output; keep a placeholder list.
            var requirementLines = Array.Empty<string>();

            items.Add(new FutGgSbcListingItemParsed(
                Title: title,
                Category: category,
                DetailsUrl: detailsUrl,
                ExpiresAtUtc: expiresAtUtc,
                RepeatableCount: repeatableCount,
                RepeatableUnlimited: repeatableUnlimited,
                RequirementLines: requirementLines));
        }

        return items;
    }

    private static DateTime? TryComputeExpiry(DateTime capturedAtUtc, string valueRaw, string unitRaw)
    {
        if (!int.TryParse(valueRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        unitRaw = unitRaw.Trim().ToLowerInvariant();
        return unitRaw.StartsWith("hour", StringComparison.Ordinal)
            ? capturedAtUtc.AddHours(value)
            : capturedAtUtc.AddDays(value);
    }
}

