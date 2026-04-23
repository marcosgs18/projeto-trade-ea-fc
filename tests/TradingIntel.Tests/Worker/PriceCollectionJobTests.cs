using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradingIntel.Application.Futbin;
using TradingIntel.Application.Persistence;
using TradingIntel.Tests.Worker.Fakes;
using TradingIntel.Worker.Health;
using TradingIntel.Worker.Jobs;
using Xunit;

namespace TradingIntel.Tests.Worker;

public sealed class PriceCollectionJobTests
{
    [Fact]
    public async Task Successful_tick_persists_normalized_snapshots_and_marks_health_success()
    {
        var market = new FakeFutbinMarketClient();
        var prices = new CapturingPlayerPriceSnapshotRepository();
        var listings = new CapturingMarketListingSnapshotRepository();
        var health = new InMemoryJobHealthRegistry();

        var options = new PriceCollectionOptions
        {
            Players = new List<PlayerWatchlistEntry>
            {
                new() { PlayerId = 1001, Name = "Player A" },
                new() { PlayerId = 1002, Name = "Player B" },
            },
        };

        var job = BuildJob(market, prices, listings, health, options);

        var result = await job.RunTickAsync(CancellationToken.None);

        result.Status.Should().Be(TickStatus.Success);
        market.CallCount.Should().Be(2);
        prices.Saved.Should().HaveCount(2);
        listings.Saved.Should().HaveCount(2);

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

        var market = new FakeFutbinMarketClient
        {
            Handler = (player, _) =>
            {
                if (player.PlayerId == 1001)
                {
                    throw new HttpRequestException("not found");
                }

                return Task.FromResult(FakeFutbinMarketClient.BuildDefaultSnapshot(player));
            },
        };

        var options = new PriceCollectionOptions
        {
            Players = new List<PlayerWatchlistEntry>
            {
                new() { PlayerId = 1001, Name = "Broken" },
                new() { PlayerId = 1002, Name = "Ok" },
            },
        };

        var job = BuildJob(market, prices, listings, health, options);

        var result = await job.RunTickAsync(CancellationToken.None);

        result.Status.Should().Be(TickStatus.Success);
        prices.Saved.Should().HaveCount(1);
        prices.Saved.Single().Player.PlayerId.Should().Be(1002);
    }

    [Fact]
    public async Task All_players_failing_is_reported_as_tick_failure_and_marks_health()
    {
        var prices = new CapturingPlayerPriceSnapshotRepository();
        var listings = new CapturingMarketListingSnapshotRepository();
        var health = new InMemoryJobHealthRegistry();

        var market = new FakeFutbinMarketClient
        {
            Handler = (_, _) => throw new HttpRequestException("upstream 503"),
        };

        var options = new PriceCollectionOptions
        {
            InitialBackoff = TimeSpan.FromSeconds(1),
            MaxBackoff = TimeSpan.FromSeconds(10),
            BackoffMultiplier = 2.0,
            Players = new List<PlayerWatchlistEntry>
            {
                new() { PlayerId = 1001 },
                new() { PlayerId = 1002 },
            },
        };

        var job = BuildJob(market, prices, listings, health, options);

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
        using var cts = new CancellationTokenSource();

        var market = new FakeFutbinMarketClient
        {
            Handler = async (_, ct) =>
            {
                cts.Cancel();
                await Task.Delay(Timeout.Infinite, ct);
                return default!;
            },
        };

        var options = new PriceCollectionOptions
        {
            Players = new List<PlayerWatchlistEntry>
            {
                new() { PlayerId = 1001 },
                new() { PlayerId = 1002 },
            },
        };

        var job = BuildJob(market, prices, listings, health, options);

        var result = await job.RunTickAsync(cts.Token);

        result.Status.Should().Be(TickStatus.Cancelled);
        prices.Saved.Should().BeEmpty();
        market.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Empty_player_watchlist_is_treated_as_successful_noop()
    {
        var market = new FakeFutbinMarketClient();
        var prices = new CapturingPlayerPriceSnapshotRepository();
        var listings = new CapturingMarketListingSnapshotRepository();
        var health = new InMemoryJobHealthRegistry();

        var options = new PriceCollectionOptions
        {
            Players = new List<PlayerWatchlistEntry>(),
        };

        var job = BuildJob(market, prices, listings, health, options);

        var result = await job.RunTickAsync(CancellationToken.None);

        result.Status.Should().Be(TickStatus.Success);
        market.CallCount.Should().Be(0);
        prices.Saved.Should().BeEmpty();
    }

    private static PriceCollectionJob BuildJob(
        IFutbinMarketClient market,
        IPlayerPriceSnapshotRepository prices,
        IMarketListingSnapshotRepository listings,
        IJobHealthRegistry health,
        PriceCollectionOptions options)
    {
        var services = new ServiceCollection();
        services.AddSingleton(market);
        services.AddScoped(_ => prices);
        services.AddScoped(_ => listings);
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        return new PriceCollectionJob(
            Options.Create(options),
            scopeFactory,
            health,
            NullLogger<PriceCollectionJob>.Instance);
    }
}
