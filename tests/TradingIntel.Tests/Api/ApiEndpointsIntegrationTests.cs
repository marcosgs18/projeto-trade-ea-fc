using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TradingIntel.Api.Contracts;
using TradingIntel.Application.Persistence;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;
using Xunit;

namespace TradingIntel.Tests.Api;

public sealed class ApiEndpointsIntegrationTests : IClassFixture<TradingIntelApiFactory>
{
    private readonly TradingIntelApiFactory _factory;

    public ApiEndpointsIntegrationTests(TradingIntelApiFactory factory)
    {
        _factory = factory;
    }

    private async Task SeedAsync(Func<IServiceProvider, Task> seed)
    {
        _ = _factory.CreateClient();
        await using var scope = _factory.Services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        await seed(scope.ServiceProvider).ConfigureAwait(false);
    }

    [Fact]
    public async Task Jobs_health_returns_snapshot()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(new Uri("/api/jobs/health", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JobsHealthResponse>();
        body.Should().NotBeNull();
        body!.Jobs.Should().NotBeNull();
    }

    [Fact]
    public async Task Sbcs_active_happy_path_and_category_filter()
    {
        await SeedAsync(async sp =>
        {
            var repo = sp.GetRequiredService<ISbcChallengeRepository>();
            var now = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
            var a = new SbcChallenge(
                Guid.NewGuid(),
                "Alpha",
                "upgrades",
                now.AddDays(14),
                SbcRepeatability.NotRepeatable(),
                "Set",
                now,
                new[] { new SbcRequirement("min_team_rating", 82) });
            var b = new SbcChallenge(
                Guid.NewGuid(),
                "Beta",
                "icons",
                now.AddDays(10),
                SbcRepeatability.NotRepeatable(),
                "Set",
                now,
                new[] { new SbcRequirement("min_team_rating", 80) });
            await repo.UpsertRangeAsync(new[] { a, b }, CancellationToken.None);
        });

        var client = _factory.CreateClient();
        var all = await client.GetAsync(new Uri("/api/sbcs/active?page=1&pageSize=10", UriKind.Relative));
        all.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await all.Content.ReadFromJsonAsync<PagedResponse<SbcChallengeResponse>>();
        page!.TotalItems.Should().BeGreaterOrEqualTo(2);

        var filtered = await client.GetAsync(
            new Uri("/api/sbcs/active?category=upgrade&page=1&pageSize=10", UriKind.Relative));
        filtered.StatusCode.Should().Be(HttpStatusCode.OK);
        var page2 = await filtered.Content.ReadFromJsonAsync<PagedResponse<SbcChallengeResponse>>();
        page2!.Items.Should().OnlyContain(c => c.Category.Contains("upgrade", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Market_prices_requires_player_and_respects_source_filter()
    {
        await SeedAsync(async sp =>
        {
            var repo = sp.GetRequiredService<IPlayerPriceSnapshotRepository>();
            var player = new PlayerReference(4242, "Listed");
            var t = new DateTime(2026, 5, 2, 10, 0, 0, DateTimeKind.Utc);
            await repo.AddRangeAsync(
                new[]
                {
                    new PlayerPriceSnapshot(player, "futbin:ps", t, new Coins(1000), null, new Coins(1000)),
                    new PlayerPriceSnapshot(player, "futbin:xbox", t, new Coins(2000), null, new Coins(2000)),
                },
                CancellationToken.None);
        });

        var client = _factory.CreateClient();
        var bad = await client.GetAsync(new Uri("/api/market/prices?from=2026-05-02T10:00:00Z&to=2026-05-02T10:00:00Z", UriKind.Relative));
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var ok = await client.GetAsync(
            new Uri(
                "/api/market/prices?playerId=4242&source=futbin:ps&from=2026-05-02T10:00:00Z&to=2026-05-02T10:00:00Z",
                UriKind.Relative));
        ok.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await ok.Content.ReadFromJsonAsync<PagedResponse<PlayerPriceResponse>>();
        page!.Items.Should().ContainSingle();
        page.Items[0].Source.Should().Be("futbin:ps");
    }

    [Fact]
    public async Task Market_listings_happy_path()
    {
        await SeedAsync(async sp =>
        {
            var repo = sp.GetRequiredService<IMarketListingSnapshotRepository>();
            var player = new PlayerReference(5151, "Lister");
            var t = new DateTime(2026, 5, 3, 8, 0, 0, DateTimeKind.Utc);
            await repo.AddRangeAsync(
                new[]
                {
                    new MarketListingSnapshot(
                        "listing-1",
                        player,
                        "futbin:ps",
                        t,
                        new Coins(500),
                        new Coins(600),
                        t.AddMinutes(10)),
                },
                CancellationToken.None);
        });

        var client = _factory.CreateClient();
        var response = await client.GetAsync(
            new Uri(
                "/api/market/listings?playerId=5151&from=2026-05-03T08:00:00Z&to=2026-05-03T08:00:00Z",
                UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<MarketListingResponse>>();
        page!.Items.Should().ContainSingle();
        page.Items[0].ListingId.Should().Be("listing-1");
    }

    [Fact]
    public async Task Opportunities_list_with_min_confidence_filter()
    {
        await SeedAsync(async sp =>
        {
            var repo = sp.GetRequiredService<ITradeOpportunityRepository>();
            var player = new PlayerReference(7777, "Opp");
            var now = DateTime.UtcNow;
            var oppId = Guid.NewGuid();
            var opp = new TradeOpportunity(
                oppId,
                player,
                now,
                new Coins(10_000),
                new Coins(12_000),
                new ConfidenceScore(0.72m),
                new[] { new OpportunityReason("TRADE_SCORE", "ok", 0.72m) },
                new[]
                {
                    new ExecutionSuggestion(Guid.NewGuid(), oppId, ExecutionAction.Buy, new Coins(10_000), now.AddMinutes(15)),
                    new ExecutionSuggestion(Guid.NewGuid(), oppId, ExecutionAction.ListForSale, new Coins(12_000), now.AddHours(24)),
                });
            await repo.UpsertAsync(opp, now, CancellationToken.None);
        });

        var client = _factory.CreateClient();
        var response = await client.GetAsync(new Uri("/api/opportunities?minConfidence=0.7&page=1&pageSize=10", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<OpportunitySummaryResponse>>();
        page!.Items.Should().ContainSingle();
        page.Items[0].Confidence.Should().Be(0.72m);
    }

    [Fact]
    public async Task Opportunities_by_id_returns_detail()
    {
        var oppId = Guid.NewGuid();
        await SeedAsync(async sp =>
        {
            var repo = sp.GetRequiredService<ITradeOpportunityRepository>();
            var player = new PlayerReference(8888, "Detail");
            var now = DateTime.UtcNow;
            var opp = new TradeOpportunity(
                oppId,
                player,
                now,
                new Coins(10_000),
                new Coins(12_000),
                new ConfidenceScore(0.55m),
                new[] { new OpportunityReason("TRADE_SCORE", "ok", 0.55m) },
                new[]
                {
                    new ExecutionSuggestion(Guid.NewGuid(), oppId, ExecutionAction.Buy, new Coins(10_000), now.AddMinutes(15)),
                    new ExecutionSuggestion(Guid.NewGuid(), oppId, ExecutionAction.ListForSale, new Coins(12_000), now.AddHours(24)),
                });
            await repo.UpsertAsync(opp, now, CancellationToken.None);
        });

        var client = _factory.CreateClient();
        var response = await client.GetAsync(new Uri($"/api/opportunities/{oppId}", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await response.Content.ReadFromJsonAsync<OpportunityDetailResponse>();
        detail!.Id.Should().Be(oppId);
        detail.Reasons.Should().NotBeEmpty();
        detail.Suggestions.Should().HaveCount(2);
    }

    [Fact]
    public async Task Opportunities_recompute_returns_summary()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            new Uri("/api/opportunities/recompute", UriKind.Relative),
            new { playerIds = Array.Empty<long>() });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await response.Content.ReadFromJsonAsync<OpportunityRecomputeResponse>();
        summary.Should().NotBeNull();
    }
}

public sealed class SwaggerIntegrationTests : IClassFixture<TradingIntelSwaggerApiFactory>
{
    private readonly TradingIntelSwaggerApiFactory _factory;

    public SwaggerIntegrationTests(TradingIntelSwaggerApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Swagger_v1_json_is_available()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(new Uri("/swagger/v1/swagger.json", UriKind.Relative));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        doc.RootElement.GetProperty("openapi").GetString().Should().StartWith("3.");
    }
}
