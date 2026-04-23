using Microsoft.Extensions.Logging;

namespace TradingIntel.Infrastructure.FutGg;

public sealed class FutGgSbcDetailParser
{
    private readonly ILogger _logger;

    public FutGgSbcDetailParser(ILogger logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<string> ParseVisibleRequirements(string renderedDetailText)
    {
        if (string.IsNullOrWhiteSpace(renderedDetailText))
        {
            _logger.LogWarning("FUT.GG detail payload is empty while parsing requirements.");
            return Array.Empty<string>();
        }

        var lines = renderedDetailText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.StartsWith("*", StringComparison.Ordinal) && line.Length > 1)
            .Select(line => line.TrimStart('*', ' '))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return lines;
    }
}

