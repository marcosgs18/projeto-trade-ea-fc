using Microsoft.Extensions.Options;
using TradingIntel.Application;
using TradingIntel.Application.Trading;
using TradingIntel.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.Configure<OpportunityRecomputeStaleSettings>(
    builder.Configuration.GetSection("Jobs:OpportunityRecompute"));
builder.Services.Configure<OpportunityRecomputePlayersSource>(
    builder.Configuration.GetSection("Jobs:OpportunityRecompute"));
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Text("TradingIntel.Api", "text/plain"));

app.MapPost(
        "/api/opportunities/recompute",
        async (
            OpportunityRecomputeHttpRequest? body,
            IOpportunityRecomputeService recompute,
            IOptionsSnapshot<OpportunityRecomputePlayersSource> playersConfig,
            CancellationToken cancellationToken) =>
        {
            var rows = playersConfig.Value.Players ?? Array.Empty<OpportunityRecomputeWatchlistRow>();
            if (rows.Count == 0)
            {
                return Results.BadRequest(new
                {
                    error = "No players configured under Jobs:OpportunityRecompute:Players.",
                });
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
                return Results.BadRequest(new { error = "No matching players for the given filter." });
            }

            var summary = await recompute.RecomputeAsync(batch, cancellationToken).ConfigureAwait(false);
            return Results.Ok(summary);
        })
    .WithName("OpportunityRecompute");

app.Run();

public partial class Program { }
