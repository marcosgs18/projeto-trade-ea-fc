using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TradingIntel.Api.Contracts;
using TradingIntel.Api.Mapping;
using TradingIntel.Application.JobHealth;
using TradingIntel.Application.Persistence;
using TradingIntel.Application.Trading;

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
                [FromServices] IOptionsSnapshot<OpportunityRecomputePlayersSource> playersConfig,
                CancellationToken cancellationToken) =>
            {
                var rows = playersConfig.Value.Players ?? Array.Empty<OpportunityRecomputeWatchlistRow>();
                if (rows.Count == 0)
                {
                    return Results.Problem(
                        detail: "No players configured under Jobs:OpportunityRecompute:Players.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                IEnumerable<OpportunityRecomputeWatchlistRow> chosen =
                    body?.PlayerIds is { Length: > 0 } ids
                        ? rows.Where(r => ids.Contains(r.PlayerId))
                        : rows;

                var batch = chosen
                    .Select(r =>
                    {
                        var name = string.IsNullOrWhiteSpace(r.Name)
                            ? $"player-{r.PlayerId}"
                            : r.Name.Trim();
                        return new OpportunityRecomputePlayer(r.PlayerId, name, r.Overall);
                    })
                    .ToList();

                if (batch.Count == 0)
                {
                    return Results.Problem(
                        detail: "No matching players for the given filter.",
                        statusCode: StatusCodes.Status400BadRequest);
                }

                var summary = await recompute.RecomputeAsync(batch, cancellationToken).ConfigureAwait(false);
                return Results.Ok(OpportunityMapper.ToResponse(summary));
            });
    }
}
