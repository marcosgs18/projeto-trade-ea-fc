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
    private static readonly Regex DetailsUrlRegex = new(
        @"\((?<url>https:\/\/www\.fut\.gg\/sbc\/(?<category>[^\/]+)\/[^)]+\/)\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex TitleLineRegex = new(
        @"^\s*###\s*\[(?<title>.+?)\s+(?<coins>[0-9,]+)!?\[Image",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ExpiresRegex = new(
        @"Expires\s+in\s+(?<value>\d+)\s+(?<unit>hours?|days?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex RepeatableRegex = new(
        @"Repeatable\s+(?<count>\d+|∞|-)",
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
        // ### [Ederson 355,950![Image ...]](https://www.fut.gg/sbc/players/26-830-ederson/)
        // ...
        // [Challenges 7]...[Expires in 20 days]...[Repeatable-]...

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith("### [", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var titleMatch = TitleLineRegex.Match(line);
            var urlMatch = DetailsUrlRegex.Match(line);

            if (!titleMatch.Success || !urlMatch.Success)
            {
                _logger.LogWarning(
                    "Failed to parse FUT.GG SBC header line. lineIndex={LineIndex} line={Line}",
                    i,
                    line);
                continue;
            }

            var title = titleMatch.Groups["title"].Value.Trim();
            var detailsUrl = urlMatch.Groups["url"].Value.Trim();
            var category = urlMatch.Groups["category"].Value.Trim();

            DateTime? expiresAtUtc = null;
            int? repeatableCount = null;
            bool repeatableUnlimited = false;

            // search a small window ahead for metadata lines
            for (var j = i; j < Math.Min(i + 12, lines.Length); j++)
            {
                var meta = lines[j];

                var expires = ExpiresRegex.Match(meta);
                if (expires.Success)
                {
                    expiresAtUtc = TryComputeExpiry(capturedAtUtc, expires.Groups["value"].Value, expires.Groups["unit"].Value);
                }

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

