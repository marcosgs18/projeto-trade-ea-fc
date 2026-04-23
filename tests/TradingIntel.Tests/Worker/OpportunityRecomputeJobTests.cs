using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradingIntel.Application.Trading;
using TradingIntel.Tests.Worker.Fakes;
using TradingIntel.Application.JobHealth;
using TradingIntel.Worker.Jobs;
using Xunit;

namespace TradingIntel.Tests.Worker;

public sealed class OpportunityRecomputeJobTests
{
    [Fact]
    public async Task Successful_tick_invokes_recompute_for_each_watchlist_player()
    {
        var health = new InMemoryJobHealthRegistry();
        var recompute = new RecordingOpportunityRecomputeService();
        var options = new OpportunityRecomputeOptions
        {
            Enabled = true,
            InitialDelay = TimeSpan.Zero,
            Interval = TimeSpan.FromHours(1),
            Players =
            {
                new OpportunityRecomputeWatchlistRow { PlayerId = 2001, Name = "A", Overall = 84 },
                new OpportunityRecomputeWatchlistRow { PlayerId = 2002, Name = "B", Overall = 85 },
            },
        };

        var services = new ServiceCollection();
        services.AddSingleton<IOpportunityRecomputeService>(recompute);
        await using var provider = services.BuildServiceProvider();

        var job = new OpportunityRecomputeJob(
            Options.Create(options),
            provider.GetRequiredService<IServiceScopeFactory>(),
            health,
            NullLogger<OpportunityRecomputeJob>.Instance);

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
        var options = new OpportunityRecomputeOptions
        {
            Enabled = true,
            InitialDelay = TimeSpan.Zero,
            Interval = TimeSpan.FromHours(1),
            Players = new List<OpportunityRecomputeWatchlistRow>(),
        };

        var services = new ServiceCollection();
        services.AddSingleton<IOpportunityRecomputeService>(recompute);
        await using var provider = services.BuildServiceProvider();

        var job = new OpportunityRecomputeJob(
            Options.Create(options),
            provider.GetRequiredService<IServiceScopeFactory>(),
            health,
            NullLogger<OpportunityRecomputeJob>.Instance);

        var result = await job.RunTickAsync(CancellationToken.None);

        result.Status.Should().Be(TickStatus.Success);
        recompute.Batches.Should().BeEmpty();
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
