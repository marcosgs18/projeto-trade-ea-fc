using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradingIntel.Application.Persistence;
using TradingIntel.Application.Trading;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;
using TradingIntel.Tests.Worker.Fakes;
using TradingIntel.Application.JobHealth;
using TradingIntel.Worker.Jobs;
using Xunit;

namespace TradingIntel.Tests.Worker;

public sealed class OpportunityRecomputeJobTests
{
    private static TrackedPlayer Player(long id, string name, int? overall) =>
        new(
            new PlayerReference(id, name),
            overall,
            WatchlistSource.Seed,
            addedAtUtc: DateTime.UtcNow,
            lastCollectedAtUtc: null,
            isActive: true);

    [Fact]
    public async Task Successful_tick_invokes_recompute_for_each_watchlist_player()
    {
        var health = new InMemoryJobHealthRegistry();
        var recompute = new RecordingOpportunityRecomputeService();
        var watchlist = new InMemoryWatchlistRepository();
        watchlist.Add(Player(2001, "A", 84));
        watchlist.Add(Player(2002, "B", 85));

        var job = BuildJob(recompute, watchlist, health);

        var result = await job.RunTickAsync(CancellationToken.None);

        result.Status.Should().Be(TickStatus.Success);
        recompute.Batches.Should().HaveCount(1);
        recompute.Batches[0].Should().HaveCount(2);
        recompute.Batches[0][0].PlayerId.Should().Be(2001);
        recompute.Batches[0][1].PlayerId.Should().Be(2002);

        var snapshot = health.Get(OpportunityRecomputeJob.Name);
        snapshot.Should().NotBeNull();
        snapshot!.LastSuccessUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Tick_with_empty_watchlist_does_not_invoke_recompute()
    {
        var health = new InMemoryJobHealthRegistry();
        var recompute = new RecordingOpportunityRecomputeService();
        var watchlist = new InMemoryWatchlistRepository();

        var job = BuildJob(recompute, watchlist, health);

        var result = await job.RunTickAsync(CancellationToken.None);

        result.Status.Should().Be(TickStatus.Success);
        recompute.Batches.Should().BeEmpty();
    }

    private static OpportunityRecomputeJob BuildJob(
        IOpportunityRecomputeService recompute,
        IWatchlistRepository watchlist,
        IJobHealthRegistry health)
    {
        var options = new OpportunityRecomputeOptions
        {
            Enabled = true,
            InitialDelay = TimeSpan.Zero,
            Interval = TimeSpan.FromHours(1),
        };

        var services = new ServiceCollection();
        services.AddSingleton<IOpportunityRecomputeService>(recompute);
        services.AddSingleton(watchlist);
        var provider = services.BuildServiceProvider();

        return new OpportunityRecomputeJob(
            Options.Create(options),
            provider.GetRequiredService<IServiceScopeFactory>(),
            health,
            NullLogger<OpportunityRecomputeJob>.Instance);
    }

    private sealed class RecordingOpportunityRecomputeService : IOpportunityRecomputeService
    {
        public List<IReadOnlyList<OpportunityRecomputePlayer>> Batches { get; } = new();

        public Task<OpportunityRecomputeSummary> RecomputeAsync(
            IReadOnlyList<OpportunityRecomputePlayer> players,
            CancellationToken cancellationToken)
        {
            Batches.Add(players);
            return Task.FromResult(new OpportunityRecomputeSummary(0, 0, 0, 0, 0));
        }
    }
}
