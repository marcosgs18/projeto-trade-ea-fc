using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradingIntel.Application.PlayerMarket;
using TradingIntel.Application.Persistence;
using TradingIntel.Tests.Worker.Fakes;
using TradingIntel.Application.JobHealth;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;
using TradingIntel.Worker.Jobs;
using Xunit;

namespace TradingIntel.Tests.Worker;

public sealed class PriceCollectionJobTests
{
    private static TrackedPlayer Player(long id, string name = "Player") =>
        new(
            new PlayerReference(id, name),
            overall: 84,
            source: WatchlistSource.Seed,
            addedAtUtc: DateTime.UtcNow,
            lastCollectedAtUtc: null,
            isActive: true);

    [Fact]
    public async Task Successful_tick_persists_normalized_snapshots_and_marks_health_success()
    {
        var market = new FakePlayerMarketClient();
        var prices = new CapturingPlayerPriceSnapshotRepository();
        var listings = new CapturingMarketListingSnapshotRepository();
        var health = new InMemoryJobHealthRegistry();
        var watchlist = new InMemoryWatchlistRepository();
        watchlist.Add(Player(1001, "Player A"));
        watchlist.Add(Player(1002, "Player B"));

        var job = BuildJob(market, prices, listings, watchlist, health);

        var result = await job.RunTickAsync(CancellationToken.None);

        result.Status.Should().Be(TickStatus.Success);
        market.CallCount.Should().Be(2);
        prices.Saved.Should().HaveCount(2);
        listings.Saved.Should().HaveCount(2);
        watchlist.LastTouchedIds.Should().BeEquivalentTo(new[] { 1001L, 1002L });

        var snapshot = health.Get(PriceCollectionJob.Name);
        snapshot.Should().NotBeNull();
        snapshot!.LastSuccessUtc.Should().NotBeNull();
        snapshot.ConsecutiveFailures.Should().Be(0);
    }

    [Fact]
    public async Task One_player_failing_does_not_abort_tick_when_others_succeed()
    {
        var prices = new CapturingPlayerPriceSnapshotRepository();
        var listings = new CapturingMarketListingSnapshotRepository();
        var health = new InMemoryJobHealthRegistry();
        var watchlist = new InMemoryWatchlistRepository();
        watchlist.Add(Player(1001, "Broken"));
        watchlist.Add(Player(1002, "Ok"));

        var market = new FakePlayerMarketClient
        {
            Handler = (player, _) =>
            {
                if (player.PlayerId == 1001)
                {
                    throw new HttpRequestException("not found");
                }

                return Task.FromResult(FakePlayerMarketClient.BuildDefaultSnapshot(player));
            },
        };

        var job = BuildJob(market, prices, listings, watchlist, health);

        var result = await job.RunTickAsync(CancellationToken.None);

        result.Status.Should().Be(TickStatus.Success);
        prices.Saved.Should().HaveCount(1);
        prices.Saved.Single().Player.PlayerId.Should().Be(1002);
        watchlist.LastTouchedIds.Should().BeEquivalentTo(new[] { 1002L });
    }

    [Fact]
    public async Task All_players_failing_is_reported_as_tick_failure_and_marks_health()
    {
        var prices = new CapturingPlayerPriceSnapshotRepository();
        var listings = new CapturingMarketListingSnapshotRepository();
        var health = new InMemoryJobHealthRegistry();
        var watchlist = new InMemoryWatchlistRepository();
        watchlist.Add(Player(1001));
        watchlist.Add(Player(1002));

        var market = new FakePlayerMarketClient
        {
            Handler = (_, _) => throw new HttpRequestException("upstream 503"),
        };

        var options = new PriceCollectionOptions
        {
            InitialBackoff = TimeSpan.FromSeconds(1),
            MaxBackoff = TimeSpan.FromSeconds(10),
            BackoffMultiplier = 2.0,
        };

        var job = BuildJob(market, prices, listings, watchlist, health, options);

        var result = await job.RunTickAsync(CancellationToken.None);

        result.Status.Should().Be(TickStatus.Failure);
        result.Error.Should().BeOfType<InvalidOperationException>();
        job.ConsecutiveFailures.Should().Be(1);
        job.CurrentBackoff.Should().Be(TimeSpan.FromSeconds(2));
        var snapshot = health.Get(PriceCollectionJob.Name)!;
        snapshot.LastFailureUtc.Should().NotBeNull();
        snapshot.ConsecutiveFailures.Should().Be(1);
    }

    [Fact]
    public async Task Cancellation_during_first_player_stops_iterating_and_returns_cancelled()
    {
        var prices = new CapturingPlayerPriceSnapshotRepository();
        var listings = new CapturingMarketListingSnapshotRepository();
        var health = new InMemoryJobHealthRegistry();
        var watchlist = new InMemoryWatchlistRepository();
        watchlist.Add(Player(1001));
        watchlist.Add(Player(1002));

        using var cts = new CancellationTokenSource();

        var market = new FakePlayerMarketClient
        {
            Handler = async (_, ct) =>
            {
                cts.Cancel();
                await Task.Delay(Timeout.Infinite, ct);
                return default!;
            },
        };

        var job = BuildJob(market, prices, listings, watchlist, health);

        var result = await job.RunTickAsync(cts.Token);

        result.Status.Should().Be(TickStatus.Cancelled);
        prices.Saved.Should().BeEmpty();
        market.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Empty_player_watchlist_is_treated_as_successful_noop()
    {
        var market = new FakePlayerMarketClient();
        var prices = new CapturingPlayerPriceSnapshotRepository();
        var listings = new CapturingMarketListingSnapshotRepository();
        var health = new InMemoryJobHealthRegistry();
        var watchlist = new InMemoryWatchlistRepository();

        var job = BuildJob(market, prices, listings, watchlist, health);

        var result = await job.RunTickAsync(CancellationToken.None);

        result.Status.Should().Be(TickStatus.Success);
        market.CallCount.Should().Be(0);
        prices.Saved.Should().BeEmpty();
        watchlist.LastTouchedIds.Should().BeEmpty();
    }

    private static PriceCollectionJob BuildJob(
        IPlayerMarketClient market,
        IPlayerPriceSnapshotRepository prices,
        IMarketListingSnapshotRepository listings,
        IWatchlistRepository watchlist,
        IJobHealthRegistry health,
        PriceCollectionOptions? options = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(market);
        services.AddScoped(_ => prices);
        services.AddScoped(_ => listings);
        services.AddSingleton(watchlist);
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        return new PriceCollectionJob(
            Options.Create(options ?? new PriceCollectionOptions()),
            scopeFactory,
            health,
            NullLogger<PriceCollectionJob>.Instance);
    }
}
