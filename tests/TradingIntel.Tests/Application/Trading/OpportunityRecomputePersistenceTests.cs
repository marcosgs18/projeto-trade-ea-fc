using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradingIntel.Application;
using TradingIntel.Application.Persistence;
using TradingIntel.Application.Sbc;
using TradingIntel.Application.Trading;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;
using TradingIntel.Infrastructure.Persistence;
using TradingIntel.Infrastructure.Persistence.Repositories;
using TradingIntel.Tests.Infrastructure.Persistence;
using Xunit;

namespace TradingIntel.Tests.Application.Trading;

/// <summary>
/// Recompute com SQLite em memória: sem edge o scoring remove a linha persistida.
/// </summary>
public sealed class OpportunityRecomputePersistenceTests : IClassFixture<PersistenceTestFixture>
{
    private readonly PersistenceTestFixture _fixture;

    public OpportunityRecomputePersistenceTests(PersistenceTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Recompute_with_null_score_removes_persisted_opportunity()
    {
        var playerId = 70_000 + Random.Shared.Next(1, 5000);
        var now = DateTime.UtcNow;
        var player = new PlayerReference(playerId, "Persistence");
        var oppId = Guid.NewGuid();
        var opportunity = new TradeOpportunity(
            oppId,
            player,
            now,
            new Coins(10_000),
            new Coins(12_000),
            new ConfidenceScore(0.5m),
            new[] { new OpportunityReason("TRADE_SCORE", "ok", 0.5m) },
            new[]
            {
                new ExecutionSuggestion(Guid.NewGuid(), oppId, ExecutionAction.Buy, new Coins(10_000), now.AddMinutes(15)),
                new ExecutionSuggestion(Guid.NewGuid(), oppId, ExecutionAction.ListForSale, new Coins(12_000), now.AddHours(24)),
            });

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddSingleton(_fixture.DbContextOptions);
        services.AddScoped<TradingIntelDbContext>(sp =>
            new TradingIntelDbContext(sp.GetRequiredService<DbContextOptions<TradingIntelDbContext>>()));
        services.AddScoped<IPlayerPriceSnapshotRepository, PlayerPriceSnapshotRepository>();
        services.AddScoped<IMarketListingSnapshotRepository, MarketListingSnapshotRepository>();
        services.AddScoped<ISbcChallengeRepository, SbcChallengeRepository>();
        services.AddScoped<ITradeOpportunityRepository, TradeOpportunityRepository>();
        services.AddSingleton<ITradeScoringService>(new StubScoringNull());
        services.AddSingleton(Options.Create(new OpportunityRecomputeStaleSettings { StaleAfter = TimeSpan.FromHours(1) }));

        await using var provider = services.BuildServiceProvider();
        await using (var scope = provider.CreateAsyncScope())
        {
            var priceRepo = scope.ServiceProvider.GetRequiredService<IPlayerPriceSnapshotRepository>();
            var oppRepo = scope.ServiceProvider.GetRequiredService<ITradeOpportunityRepository>();

            await priceRepo.AddRangeAsync(
                new[]
                {
                    new PlayerPriceSnapshot(
                        player,
                        "futbin:ps",
                        now,
                        new Coins(10_000),
                        null,
                        new Coins(10_500)),
                },
                CancellationToken.None);

            await oppRepo.UpsertAsync(opportunity, now, CancellationToken.None);
            (await oppRepo.ExistsForPlayerAsync(playerId, CancellationToken.None)).Should().BeTrue();
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var svc = new OpportunityRecomputeService(
                scope.ServiceProvider.GetRequiredService<IPlayerPriceSnapshotRepository>(),
                scope.ServiceProvider.GetRequiredService<IMarketListingSnapshotRepository>(),
                scope.ServiceProvider.GetRequiredService<ISbcChallengeRepository>(),
                new RatingBandDemandService(NullLogger<RatingBandDemandService>.Instance),
                scope.ServiceProvider.GetRequiredService<ITradeScoringService>(),
                scope.ServiceProvider.GetRequiredService<ITradeOpportunityRepository>(),
                scope.ServiceProvider.GetRequiredService<IOptions<OpportunityRecomputeStaleSettings>>(),
                NullLogger<OpportunityRecomputeService>.Instance);

            await svc.RecomputeAsync(
                new[] { new OpportunityRecomputePlayer(playerId, "Persistence", 84) },
                CancellationToken.None);

            var oppRepo = scope.ServiceProvider.GetRequiredService<ITradeOpportunityRepository>();
            (await oppRepo.ExistsForPlayerAsync(playerId, CancellationToken.None)).Should().BeFalse();
        }
    }

    private sealed class StubScoringNull : ITradeScoringService
    {
        public TradeOpportunity? Score(TradeScoringInput input, TradeScoringWeights? weights = null) => null;
    }
}
