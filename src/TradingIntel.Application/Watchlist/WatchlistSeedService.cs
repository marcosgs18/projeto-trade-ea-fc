using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingIntel.Application.Persistence;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;

namespace TradingIntel.Application.Watchlist;

/// <summary>
/// Default <see cref="IWatchlistSeedService"/>. Reads the on-disk catalog
/// JSON, merges it with entries imported from the legacy <c>appsettings</c>
/// arrays (passed by the host so this service does not need to know the
/// jobs configuration shape), deduplicates by <c>PlayerId</c> with a clear
/// precedence (Api &gt; AppSettings &gt; Seed), and upserts each surviving
/// entry into <c>tracked_players</c>.
/// </summary>
public sealed class WatchlistSeedService : IWatchlistSeedService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IWatchlistRepository _watchlist;
    private readonly IOptions<WatchlistSeedOptions> _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<WatchlistSeedService> _logger;

    public WatchlistSeedService(
        IWatchlistRepository watchlist,
        IOptions<WatchlistSeedOptions> options,
        TimeProvider clock,
        ILogger<WatchlistSeedService> logger)
    {
        _watchlist = watchlist ?? throw new ArgumentNullException(nameof(watchlist));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<WatchlistSeedReport> SeedAsync(
        IEnumerable<WatchlistSeedEntry> appSettingsEntries,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(appSettingsEntries);

        var settings = _options.Value;

        var catalogEntries = await LoadCatalogAsync(settings, cancellationToken).ConfigureAwait(false);
        var appSettingsList = appSettingsEntries.ToList();

        // Precedence: AppSettings overrides Seed (operator config "wins" over the
        // bundled seed). Api source is not part of the boot pipeline — operators
        // add those rows at runtime through the API; we never overwrite them
        // from a startup seed (they are skipped on conflict below).
        var byPlayerId = new Dictionary<long, WatchlistSeedEntry>(catalogEntries.Count + appSettingsList.Count);
        foreach (var entry in catalogEntries)
        {
            byPlayerId[entry.PlayerId] = entry;
        }
        foreach (var entry in appSettingsList)
        {
            byPlayerId[entry.PlayerId] = entry;
        }

        var inserted = 0;
        var updated = 0;
        var skipped = 0;
        var nowUtc = _clock.GetUtcNow().UtcDateTime;

        foreach (var entry in byPlayerId.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.PlayerId <= 0 || string.IsNullOrWhiteSpace(entry.DisplayName))
            {
                skipped++;
                _logger.LogWarning(
                    "WatchlistSeed: skipping invalid entry. playerId={PlayerId} displayName='{DisplayName}'",
                    entry.PlayerId,
                    entry.DisplayName);
                continue;
            }

            var existing = await _watchlist
                .GetByPlayerIdAsync(entry.PlayerId, cancellationToken)
                .ConfigureAwait(false);

            // Never let the seed step on a row added by an operator through the API
            // — they almost always carry context the seed file does not (e.g. a
            // freshly promoted player). Just refresh display name / overall.
            if (existing is { Source: WatchlistSource.Api })
            {
                var refreshed = new TrackedPlayer(
                    new PlayerReference(entry.PlayerId, entry.DisplayName),
                    entry.Overall ?? existing.Overall,
                    existing.Source,
                    existing.AddedAtUtc,
                    existing.LastCollectedAtUtc,
                    existing.IsActive);
                await _watchlist.UpsertAsync(refreshed, cancellationToken).ConfigureAwait(false);
                updated++;
                continue;
            }

            if (existing is null)
            {
                var inserted_ = new TrackedPlayer(
                    new PlayerReference(entry.PlayerId, entry.DisplayName),
                    entry.Overall,
                    entry.Source,
                    nowUtc,
                    null,
                    isActive: true);
                await _watchlist.UpsertAsync(inserted_, cancellationToken).ConfigureAwait(false);
                inserted++;
            }
            else
            {
                var updated_ = new TrackedPlayer(
                    new PlayerReference(entry.PlayerId, entry.DisplayName),
                    entry.Overall ?? existing.Overall,
                    existing.Source,
                    existing.AddedAtUtc,
                    existing.LastCollectedAtUtc,
                    existing.IsActive);
                await _watchlist.UpsertAsync(updated_, cancellationToken).ConfigureAwait(false);
                updated++;
            }
        }

        var report = new WatchlistSeedReport(
            CatalogEntriesRead: catalogEntries.Count,
            AppSettingsEntriesRead: appSettingsList.Count,
            Inserted: inserted,
            Updated: updated,
            Skipped: skipped);

        _logger.LogInformation(
            "WatchlistSeed: done. catalog={CatalogRead} appSettings={AppSettingsRead} inserted={Inserted} updated={Updated} skipped={Skipped}",
            report.CatalogEntriesRead,
            report.AppSettingsEntriesRead,
            report.Inserted,
            report.Updated,
            report.Skipped);

        return report;
    }

    private async Task<IReadOnlyList<WatchlistSeedEntry>> LoadCatalogAsync(
        WatchlistSeedOptions settings,
        CancellationToken cancellationToken)
    {
        var rawPath = settings.CatalogSeedPath;
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            _logger.LogDebug("WatchlistSeed: CatalogSeedPath is empty; skipping JSON catalog load.");
            return Array.Empty<WatchlistSeedEntry>();
        }

        var fullPath = Path.IsPathRooted(rawPath)
            ? rawPath
            : Path.Combine(AppContext.BaseDirectory, rawPath);

        if (!File.Exists(fullPath))
        {
            if (settings.RequireCatalogSeed)
            {
                throw new FileNotFoundException(
                    $"Watchlist catalog seed not found at '{fullPath}' and Watchlist:RequireCatalogSeed is true.",
                    fullPath);
            }

            _logger.LogWarning(
                "WatchlistSeed: catalog file not found at {Path}; continuing with appsettings entries only.",
                fullPath);
            return Array.Empty<WatchlistSeedEntry>();
        }

        CatalogFileDto? dto;
        try
        {
            await using var stream = File.OpenRead(fullPath);
            dto = await JsonSerializer
                .DeserializeAsync<CatalogFileDto>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            if (settings.RequireCatalogSeed)
            {
                throw;
            }

            _logger.LogError(
                ex,
                "WatchlistSeed: failed to parse catalog file at {Path}; continuing with appsettings entries only.",
                fullPath);
            return Array.Empty<WatchlistSeedEntry>();
        }

        if (dto?.Players is null || dto.Players.Count == 0)
        {
            return Array.Empty<WatchlistSeedEntry>();
        }

        var entries = new List<WatchlistSeedEntry>(dto.Players.Count);
        foreach (var p in dto.Players)
        {
            if (p is null)
            {
                continue;
            }

            entries.Add(new WatchlistSeedEntry(
                p.PlayerId,
                p.DisplayName ?? string.Empty,
                p.Overall,
                WatchlistSource.Seed));
        }

        return entries;
    }

    private sealed class CatalogFileDto
    {
        public int Version { get; set; }
        public List<CatalogPlayerDto>? Players { get; set; }
    }

    private sealed class CatalogPlayerDto
    {
        public long PlayerId { get; set; }
        public string? DisplayName { get; set; }
        public int? Overall { get; set; }
    }
}
