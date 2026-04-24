using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TradingIntel.Application.FutGg;
using TradingIntel.Application.Persistence;
using TradingIntel.Tests.Worker.Fakes;
using TradingIntel.Application.JobHealth;
using TradingIntel.Worker.Jobs;
using Xunit;

namespace TradingIntel.Tests.Worker;

public sealed class SbcCollectionJobTests
{
    [Fact]
    public async Task Successful_tick_marks_health_as_success()
    {
        var fakeClient = new FakeFutGgSbcClient();
        var health = new InMemoryJobHealthRegistry();
        var job = BuildJob(fakeClient, health, new SbcCollectionOptions());

        var result = await job.RunTickAsync(CancellationToken.None);

        result.Status.Should().Be(TickStatus.Success);
        fakeClient.CallCount.Should().Be(1);

        var snapshot = health.Get(SbcCollectionJob.Name);
        snapshot.Should().NotBeNull();
        snapshot!.LastSuccessUtc.Should().NotBeNull();
        snapshot.LastFailureUtc.Should().BeNull();
        snapshot.ConsecutiveFailures.Should().Be(0);
    }

    [Fact]
    public async Task Client_failure_is_captured_and_backoff_advances()
    {
        var fakeClient = new FakeFutGgSbcClient
        {
            Handler = (_) => throw new HttpRequestException("upstream 503"),
        };
        var health = new InMemoryJobHealthRegistry();
        var options = new SbcCollectionOptions
        {
            InitialBackoff = TimeSpan.FromSeconds(1),
            MaxBackoff = TimeSpan.FromSeconds(10),
            BackoffMultiplier = 2.0,
        };
        var job = BuildJob(fakeClient, health, options);

        var first = await job.RunTickAsync(CancellationToken.None);
        var backoffAfterFirst = job.CurrentBackoff;
        var second = await job.RunTickAsync(CancellationToken.None);

        first.Status.Should().Be(TickStatus.Failure);
        second.Status.Should().Be(TickStatus.Failure);

        backoffAfterFirst.Should().Be(TimeSpan.FromSeconds(2));
        job.CurrentBackoff.Should().Be(TimeSpan.FromSeconds(4));
        job.ConsecutiveFailures.Should().Be(2);

        var snapshot = health.Get(SbcCollectionJob.Name)!;
        snapshot.ConsecutiveFailures.Should().Be(2);
        snapshot.LastFailureMessage.Should().Contain("upstream 503");
        snapshot.LastSuccessUtc.Should().BeNull();
    }

    [Fact]
    public async Task Cancellation_during_fetch_returns_cancelled_status()
    {
        using var cts = new CancellationTokenSource();
        var fakeClient = new FakeFutGgSbcClient
        {
            Handler = async (ct) =>
            {
                cts.Cancel();
                await Task.Delay(Timeout.Infinite, ct);
                return FakeFutGgSbcClient.BuildDefaultSnapshot();
            },
        };
        var health = new InMemoryJobHealthRegistry();
        var job = BuildJob(fakeClient, health, new SbcCollectionOptions());

        var result = await job.RunTickAsync(cts.Token);

        result.Status.Should().Be(TickStatus.Cancelled);
        result.Error.Should().BeNull();

        var snapshot = health.Get(SbcCollectionJob.Name);
        // Cancellation is not recorded as success or failure.
        (snapshot is null || (snapshot.LastSuccessUtc is null && snapshot.LastFailureUtc is null))
            .Should().BeTrue();
    }

    [Fact]
    public async Task Backoff_is_capped_at_MaxBackoff_after_consecutive_failures()
    {
        var fakeClient = new FakeFutGgSbcClient
        {
            Handler = (_) => throw new InvalidOperationException("boom"),
        };
        var health = new InMemoryJobHealthRegistry();
        var options = new SbcCollectionOptions
        {
            InitialBackoff = TimeSpan.FromSeconds(1),
            MaxBackoff = TimeSpan.FromSeconds(5),
            BackoffMultiplier = 3.0,
        };
        var job = BuildJob(fakeClient, health, options);

        for (int i = 0; i < 10; i++)
        {
            await job.RunTickAsync(CancellationToken.None);
        }

        job.CurrentBackoff.Should().BeLessThanOrEqualTo(options.MaxBackoff);
        job.CurrentBackoff.Should().Be(options.MaxBackoff);
        job.ConsecutiveFailures.Should().Be(10);
    }

    [Fact]
    public async Task Successful_tick_after_failures_resets_backoff_and_failure_count()
    {
        bool shouldFail = true;
        var fakeClient = new FakeFutGgSbcClient
        {
            Handler = (_) =>
            {
                if (shouldFail)
                {
                    throw new HttpRequestException("transient");
                }

                return Task.FromResult(FakeFutGgSbcClient.BuildDefaultSnapshot());
            },
        };
        var health = new InMemoryJobHealthRegistry();
        var options = new SbcCollectionOptions
        {
            InitialBackoff = TimeSpan.FromSeconds(1),
            MaxBackoff = TimeSpan.FromSeconds(10),
            BackoffMultiplier = 2.0,
        };
        var job = BuildJob(fakeClient, health, options);

        await job.RunTickAsync(CancellationToken.None);
        await job.RunTickAsync(CancellationToken.None);
        shouldFail = false;
        var finalResult = await job.RunTickAsync(CancellationToken.None);

        finalResult.Status.Should().Be(TickStatus.Success);
        job.ConsecutiveFailures.Should().Be(0);
        job.CurrentBackoff.Should().Be(options.InitialBackoff);

        var snapshot = health.Get(SbcCollectionJob.Name)!;
        snapshot.ConsecutiveFailures.Should().Be(0);
        snapshot.LastSuccessUtc.Should().NotBeNull();
        snapshot.LastFailureUtc.Should().NotBeNull(); // historical failure kept for diagnostics
    }

    [Fact]
    public async Task Successful_tick_persists_snapshot_via_repository_and_is_idempotent()
    {
        var snapshot = FakeFutGgSbcClient.BuildDefaultSnapshot(challengeCount: 3);
        var fakeClient = new FakeFutGgSbcClient
        {
            Handler = (_) => Task.FromResult(snapshot),
        };
        var repo = new CapturingSbcChallengeRepository();
        var health = new InMemoryJobHealthRegistry();
        var job = BuildJob(fakeClient, health, new SbcCollectionOptions(), repo);

        var first = await job.RunTickAsync(CancellationToken.None);
        var second = await job.RunTickAsync(CancellationToken.None);

        first.Status.Should().Be(TickStatus.Success);
        second.Status.Should().Be(TickStatus.Success);

        repo.UpsertCalls.Should().HaveCount(2);
        repo.UpsertCalls[0].Should().BeEquivalentTo(snapshot.Challenges);
        repo.UpsertCalls[1].Should().BeEquivalentTo(snapshot.Challenges);
        // Upsert is idempotent by Id: two ticks with the same payload leave
        // the store holding exactly one row per challenge.
        repo.State.Should().HaveCount(snapshot.Challenges.Count);
        repo.State.Keys.Should().BeEquivalentTo(snapshot.Challenges.Select(c => c.Id));
    }

    private static SbcCollectionJob BuildJob(
        IFutGgSbcClient client,
        IJobHealthRegistry health,
        SbcCollectionOptions options,
        ISbcChallengeRepository? sbcRepository = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(client);
        services.AddSingleton<ISbcChallengeRepository>(sbcRepository ?? new CapturingSbcChallengeRepository());
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        return new SbcCollectionJob(
            Options.Create(options),
            scopeFactory,
            health,
            NullLogger<SbcCollectionJob>.Instance);
    }
}
