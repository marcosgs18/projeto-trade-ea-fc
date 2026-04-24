using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TradingIntel.Application.Watchlist;
using TradingIntel.Domain.ValueObjects;

namespace TradingIntel.Infrastructure.Watchlist;

/// <summary>
/// Host-side glue that converts the legacy <c>Jobs:PriceCollection:Players</c>
/// and <c>Jobs:OpportunityRecompute:Players</c> arrays in <c>appsettings</c>
/// into <see cref="WatchlistSeedEntry"/> instances and invokes
/// <see cref="IWatchlistSeedService.SeedAsync"/>. Lives in Infrastructure (and
/// not in Application) because it depends on <see cref="IConfiguration"/> —
/// keeping the Application port free of hosting concerns.
/// </summary>
public static class WatchlistBootstrapper
{
    public static async Task<WatchlistSeedReport> SeedWatchlistAsync(
        this IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var appSettingsEntries = ReadLegacyPlayers(configuration);

        using var scope = services.CreateScope();
        var seed = scope.ServiceProvider.GetRequiredService<IWatchlistSeedService>();
        return await seed.SeedAsync(appSettingsEntries, cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<WatchlistSeedEntry> ReadLegacyPlayers(IConfiguration configuration)
    {
        var sections = new[]
        {
            "Jobs:PriceCollection:Players",
            "Jobs:OpportunityRecompute:Players",
        };

        var entries = new List<WatchlistSeedEntry>();
        foreach (var path in sections)
        {
            var section = configuration.GetSection(path);
            foreach (var child in section.GetChildren())
            {
                if (!long.TryParse(child["PlayerId"], out var playerId) || playerId <= 0)
                {
                    continue;
                }

                var name = child["Name"];
                var displayName = string.IsNullOrWhiteSpace(name) ? $"player-{playerId}" : name!.Trim();
                var overall = int.TryParse(child["Overall"], out var ov) ? ov : (int?)null;

                entries.Add(new WatchlistSeedEntry(
                    playerId,
                    displayName,
                    overall,
                    WatchlistSource.AppSettings));
            }
        }

        return entries;
    }
}
