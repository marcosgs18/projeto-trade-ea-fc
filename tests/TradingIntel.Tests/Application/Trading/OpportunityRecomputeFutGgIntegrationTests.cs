using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradingIntel.Application;
using TradingIntel.Application.Persistence;
using TradingIntel.Application.PlayerMarket;
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
/// Garantia de regressão para o bug em que o recompute filtrava só por
/// <c>Source = futbin:*</c> enquanto o adapter ativo (FUT.GG) gravava com
/// <c>Source = futgg:pc</c>. O teste exercita o caminho real ponta a ponta:
/// SQLite + repositórios concretos + scoring real + <see cref="MarketSourceOptions"/>
/// configurado para <c>futgg</c>; ele deve materializar uma <see cref="TradeOpportunity"/>
/// na tabela <c>trade_opportunities</c>.
/// </summary>
public sealed class OpportunityRecomputeFutGgIntegrationTests : IClassFixture<PersistenceTestFixture>
{
    private readonly PersistenceTestFixture _fixture;

    public OpportunityRecomputeFutGgIntegrationTests(PersistenceTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Recompute_with_futgg_source_persists_trade_opportunity()
    {
        var playerId = 80_000 + Random.Shared.Next(1, 5_000);
        var now = DateTime.UtcNow;
        var player = new PlayerReference(playerId, "FutGg Subject");

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
        services.AddSingleton(Options.Create(new OpportunityRecomputeStaleSettings { StaleAfter = TimeSpan.FromHours(1) }));
        services.AddSingleton(Options.Create(new MarketSourceOptions { Source = "futgg" }));

        await using var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var priceRepo = scope.ServiceProvider.GetRequiredService<IPlayerPriceSnapshotRepository>();
            var listingRepo = scope.ServiceProvider.GetRequiredService<IMarketListingSnapshotRepository>();
            var sbcRepo = scope.ServiceProvider.GetRequiredService<ISbcChallengeRepository>();

            await priceRepo.AddRangeAsync(
                new[]
                {
                    new PlayerPriceSnapshot(
                        player,
                        "futgg:pc",
                        now,
                        new Coins(10_000),
                        new Coins(12_000),
                        new Coins(11_500)),
                },
                CancellationToken.None);

            await listingRepo.AddRangeAsync(
                new[]
                {
                    new MarketListingSnapshot(
                        $"listing-{playerId}-1",
                        player,
                        "futgg:pc",
                        now,
                        new Coins(9_500),
                        new Coins(10_000),
                        now.AddHours(1)),
                },
                CancellationToken.None);

            await sbcRepo.UpsertRangeAsync(
                new[]
                {
                    new SbcChallenge(
                        Guid.NewGuid(),
                        "84+ Upgrade",
                        "upgrades",
                        now.AddHours(36),
                        SbcRepeatability.NotRepeatable(),
                        "set-upgrades",
                        now,
                        new[] { new SbcRequirement("min_team_rating", 84) }),
                },
                CancellationToken.None);
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var svc = new OpportunityRecomputeService(
                scope.ServiceProvider.GetRequiredService<IPlayerPriceSnapshotRepository>(),
                scope.ServiceProvider.GetRequiredService<IMarketListingSnapshotRepository>(),
                scope.ServiceProvider.GetRequiredService<ISbcChallengeRepository>(),
                new RatingBandDemandService(NullLogger<RatingBandDemandService>.Instance),
                new TradeScoringService(NullLogger<TradeScoringService>.Instance),
                scope.ServiceProvider.GetRequiredService<ITradeOpportunityRepository>(),
                scope.ServiceProvider.GetRequiredService<IOptions<OpportunityRecomputeStaleSettings>>(),
                scope.ServiceProvider.GetRequiredService<IOptions<MarketSourceOptions>>(),
                NullLogger<OpportunityRecomputeService>.Instance);

            var summary = await svc.RecomputeAsync(
                new[] { new OpportunityRecomputePlayer(playerId, "FutGg Subject", 84) },
                CancellationToken.None);

            summary.Upserted.Should().Be(1);
            summary.SkippedMissingPrice.Should().Be(0);

            var oppRepo = scope.ServiceProvider.GetRequiredService<ITradeOpportunityRepository>();
            var exists = await oppRepo.ExistsForPlayerAsync(playerId, CancellationToken.None);
            exists.Should().BeTrue("o recompute com Market:Source=futgg deve materializar a oportunidade quando há preço futgg:pc + SBC ativo");
        }
    }
}
