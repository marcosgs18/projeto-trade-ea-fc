using Microsoft.AspNetCore.Mvc;
using TradingIntel.Api.Contracts;
using TradingIntel.Api.Mapping;
using TradingIntel.Application.JobHealth;
using TradingIntel.Application.Persistence;
using TradingIntel.Application.Trading;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;

namespace TradingIntel.Api;

public static class TradingIntelApiEndpoints
{
    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/jobs/health", (IJobHealthRegistry registry) =>
            Results.Ok(JobHealthMapper.ToResponse(registry.Snapshot())));

        api.MapGet(
            "/sbcs/active",
            async (
                [FromServices] ISbcChallengeRepository repo,
                [FromQuery] DateTime? expiresBefore,
                [FromQuery] string? category,
                [FromQuery] int? requiresOverall,
                [FromQuery] bool? includeExpired,
                [FromQuery] int? page,
                [FromQuery] int? pageSize,
                CancellationToken cancellationToken) =>
            {
                if (expiresBefore is { Kind: not DateTimeKind.Utc })
                {
                    return Results.Problem(
                        detail: "expiresBefore must be UTC.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                if (requiresOverall is < 0 or > 99)
                {
                    return Results.Problem(
                        detail: "requiresOverall must be between 0 and 99.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                var now = DateTime.UtcNow;
                var (p, ps, skip) = Pagination.Normalize(page, pageSize);
                var query = new SbcActiveListQuery(
                    ActiveAsOfUtc: now,
                    CategoryContains: category,
                    ExpiresBeforeUtc: expiresBefore,
                    RequiresOverall: requiresOverall,
                    IncludeExpired: includeExpired ?? false);

                var (items, total) = await repo
                    .QueryActivePagedAsync(query, skip, ps, cancellationToken)
                    .ConfigureAwait(false);

                var dto = items.Select(SbcMapper.ToResponse).ToList();
                return Results.Ok(
                    new PagedResponse<SbcChallengeResponse>(
                        dto,
                        p,
                        ps,
                        total,
                        Pagination.TotalPages(total, ps)));
            });

        api.MapGet(
            "/market/prices",
            async (
                [FromServices] IPlayerPriceSnapshotRepository repo,
                [FromQuery] long? playerId,
                [FromQuery] string? source,
                [FromQuery] DateTime? from,
                [FromQuery] DateTime? to,
                [FromQuery] int? page,
                [FromQuery] int? pageSize,
                CancellationToken cancellationToken) =>
            {
                if (playerId is null or <= 0)
                {
                    return Results.Problem(
                        detail: "playerId is required.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                if (from is null || to is null)
                {
                    return Results.Problem(
                        detail: "from and to are required (UTC).",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                if (from.Value.Kind != DateTimeKind.Utc || to.Value.Kind != DateTimeKind.Utc)
                {
                    return Results.Problem(
                        detail: "from and to must be UTC.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                if (from > to)
                {
                    return Results.Problem(
                        detail: "from must be less than or equal to to.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                var (p, ps, skip) = Pagination.Normalize(page, pageSize);
                var (items, total) = await repo
                    .GetByPlayerPagedAsync(playerId.Value, source, from.Value, to.Value, skip, ps, cancellationToken)
                    .ConfigureAwait(false);

                var dto = items.Select(MarketMapper.ToResponse).ToList();
                return Results.Ok(
                    new PagedResponse<PlayerPriceResponse>(
                        dto,
                        p,
                        ps,
                        total,
                        Pagination.TotalPages(total, ps)));
            });

        api.MapGet(
            "/market/listings",
            async (
                [FromServices] IMarketListingSnapshotRepository repo,
                [FromQuery] long? playerId,
                [FromQuery] DateTime? from,
                [FromQuery] DateTime? to,
                [FromQuery] int? page,
                [FromQuery] int? pageSize,
                CancellationToken cancellationToken) =>
            {
                if (playerId is null or <= 0)
                {
                    return Results.Problem(
                        detail: "playerId is required.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                if (from is null || to is null)
                {
                    return Results.Problem(
                        detail: "from and to are required (UTC).",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                if (from.Value.Kind != DateTimeKind.Utc || to.Value.Kind != DateTimeKind.Utc)
                {
                    return Results.Problem(
                        detail: "from and to must be UTC.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                if (from > to)
                {
                    return Results.Problem(
                        detail: "from must be less than or equal to to.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                var (p, ps, skip) = Pagination.Normalize(page, pageSize);
                var (items, total) = await repo
                    .GetByPlayerPagedAsync(playerId.Value, from.Value, to.Value, skip, ps, cancellationToken)
                    .ConfigureAwait(false);

                var dto = items.Select(MarketMapper.ToResponse).ToList();
                return Results.Ok(
                    new PagedResponse<MarketListingResponse>(
                        dto,
                        p,
                        ps,
                        total,
                        Pagination.TotalPages(total, ps)));
            });

        api.MapGet(
            "/opportunities",
            async (
                [FromServices] ITradeOpportunityRepository repo,
                [FromQuery] decimal? minConfidence,
                [FromQuery] decimal? minNetMargin,
                [FromQuery] long? playerId,
                [FromQuery] DateTime? detectedAfter,
                [FromQuery] int? page,
                [FromQuery] int? pageSize,
                CancellationToken cancellationToken) =>
            {
                if (minConfidence is < 0 or > 1)
                {
                    return Results.Problem(
                        detail: "minConfidence must be between 0 and 1.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                if (minNetMargin is < 0)
                {
                    return Results.Problem(
                        detail: "minNetMargin must be non-negative.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                if (detectedAfter is { Kind: not DateTimeKind.Utc })
                {
                    return Results.Problem(
                        detail: "detectedAfter must be UTC.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                var (p, ps, skip) = Pagination.Normalize(page, pageSize);
                var filter = new TradeOpportunityListFilter(minConfidence, minNetMargin, playerId, detectedAfter);
                var (items, total) = await repo
                    .QueryPagedAsync(filter, skip, ps, cancellationToken)
                    .ConfigureAwait(false);

                var dto = items.Select(OpportunityMapper.ToSummary).ToList();
                return Results.Ok(
                    new PagedResponse<OpportunitySummaryResponse>(
                        dto,
                        p,
                        ps,
                        total,
                        Pagination.TotalPages(total, ps)));
            });

        api.MapGet(
            "/opportunities/{id:guid}",
            async (
                [FromServices] ITradeOpportunityRepository repo,
                Guid id,
                CancellationToken cancellationToken) =>
            {
                var view = await repo.GetByOpportunityIdAsync(id, cancellationToken).ConfigureAwait(false);
                if (view is null)
                {
                    return Results.Problem(statusCode: StatusCodes.Status404NotFound, detail: "Opportunity not found.");
                }

                return Results.Ok(OpportunityMapper.ToDetail(view));
            });

        api.MapPost(
            "/opportunities/recompute",
            async (
                [FromBody] OpportunityRecomputeHttpRequest? body,
                [FromServices] IOpportunityRecomputeService recompute,
                [FromServices] IWatchlistRepository watchlist,
                CancellationToken cancellationToken) =>
            {
                var active = await watchlist.GetActiveAsync(cancellationToken).ConfigureAwait(false);
                if (active.Count == 0)
                {
                    return Results.Problem(
                        detail: "Watchlist is empty (tracked_players has no active rows). " +
                                "Add entries via POST /api/watchlist or seed them in data/players-catalog.seed.json.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                IEnumerable<TrackedPlayer> chosen = body?.PlayerIds is { Length: > 0 } ids
                    ? active.Where(p => ids.Contains(p.Player.PlayerId))
                    : active;

                var batch = chosen
                    .Select(p => new OpportunityRecomputePlayer(p.Player.PlayerId, p.Player.DisplayName, p.Overall))
                    .ToList();

                if (batch.Count == 0)
                {
                    return Results.Problem(
                        detail: "No matching players in the watchlist for the given filter.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                var summary = await recompute.RecomputeAsync(batch, cancellationToken).ConfigureAwait(false);
                return Results.Ok(OpportunityMapper.ToResponse(summary));
            });

        MapWatchlist(api);
    }

    private static void MapWatchlist(RouteGroupBuilder api)
    {
        api.MapGet(
            "/watchlist",
            async (
                [FromServices] IWatchlistRepository repo,
                [FromQuery] bool? includeInactive,
                [FromQuery] string? source,
                [FromQuery] int? minOverall,
                [FromQuery] int? page,
                [FromQuery] int? pageSize,
                CancellationToken cancellationToken) =>
            {
                if (minOverall is < 0 or > 99)
                {
                    return Results.Problem(
                        detail: "minOverall must be between 0 and 99.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                WatchlistSource? parsedSource = null;
                if (!string.IsNullOrWhiteSpace(source))
                {
                    if (!Enum.TryParse<WatchlistSource>(source, ignoreCase: true, out var parsed))
                    {
                        return Results.Problem(
                            detail: $"source must be one of: {string.Join(", ", Enum.GetNames<WatchlistSource>())}.",
                            statusCode: StatusCodes.Status400BadRequest);
                    }

                    parsedSource = parsed;
                }

                var (p, ps, skip) = Pagination.Normalize(page, pageSize);
                var query = new WatchlistQuery(includeInactive ?? false, parsedSource, minOverall);
                var (items, total) = await repo
                    .QueryPagedAsync(query, skip, ps, cancellationToken)
                    .ConfigureAwait(false);

                var dto = items.Select(WatchlistMapper.ToResponse).ToList();
                return Results.Ok(
                    new PagedResponse<TrackedPlayerResponse>(
                        dto,
                        p,
                        ps,
                        total,
                        Pagination.TotalPages(total, ps)));
            });

        api.MapGet(
            "/watchlist/{playerId:long}",
            async (
                [FromServices] IWatchlistRepository repo,
                long playerId,
                CancellationToken cancellationToken) =>
            {
                if (playerId <= 0)
                {
                    return Results.Problem(
                        detail: "playerId must be greater than zero.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                var player = await repo.GetByPlayerIdAsync(playerId, cancellationToken).ConfigureAwait(false);
                if (player is null)
                {
                    return Results.Problem(
                        detail: $"No watchlist entry for playerId={playerId}.",
                        statusCode: StatusCodes.Status404NotFound);
                }

                return Results.Ok(WatchlistMapper.ToResponse(player));
            });

        api.MapPost(
            "/watchlist",
            async (
                [FromBody] CreateTrackedPlayerRequest? body,
                [FromServices] IWatchlistRepository repo,
                CancellationToken cancellationToken) =>
            {
                if (body is null)
                {
                    return Results.Problem(
                        detail: "Request body is required.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                if (body.PlayerId <= 0)
                {
                    return Results.Problem(
                        detail: "playerId must be greater than zero.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                if (string.IsNullOrWhiteSpace(body.DisplayName))
                {
                    return Results.Problem(
                        detail: "displayName is required.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                if (body.Overall is < 0 or > 99)
                {
                    return Results.Problem(
                        detail: "overall must be between 0 and 99 when provided.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                var existing = await repo.GetByPlayerIdAsync(body.PlayerId, cancellationToken).ConfigureAwait(false);
                TrackedPlayer toUpsert;

                if (existing is null)
                {
                    toUpsert = new TrackedPlayer(
                        new PlayerReference(body.PlayerId, body.DisplayName.Trim()),
                        body.Overall,
                        WatchlistSource.Api,
                        DateTime.UtcNow,
                        null,
                        isActive: true);
                }
                else
                {
                    toUpsert = new TrackedPlayer(
                        new PlayerReference(body.PlayerId, body.DisplayName.Trim()),
                        body.Overall ?? existing.Overall,
                        existing.Source,
                        existing.AddedAtUtc,
                        existing.LastCollectedAtUtc,
                        isActive: true);
                }

                var saved = await repo.UpsertAsync(toUpsert, cancellationToken).ConfigureAwait(false);
                var response = WatchlistMapper.ToResponse(saved);

                return existing is null
                    ? Results.Created($"/api/watchlist/{saved.Player.PlayerId}", response)
                    : Results.Ok(response);
            });

        api.MapDelete(
            "/watchlist/{playerId:long}",
            async (
                [FromServices] IWatchlistRepository repo,
                long playerId,
                CancellationToken cancellationToken) =>
            {
                if (playerId <= 0)
                {
                    return Results.Problem(
                        detail: "playerId must be greater than zero.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                var ok = await repo.DeactivateAsync(playerId, cancellationToken).ConfigureAwait(false);
                return ok
                    ? Results.NoContent()
                    : Results.Problem(
                        detail: $"No watchlist entry for playerId={playerId}.",
                        statusCode: StatusCodes.Status404NotFound);
            });
    }
}
