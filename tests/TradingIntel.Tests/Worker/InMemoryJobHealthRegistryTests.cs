using FluentAssertions;
using TradingIntel.Worker.Health;
using Xunit;

namespace TradingIntel.Tests.Worker;

public sealed class InMemoryJobHealthRegistryTests
{
    [Fact]
    public void Get_returns_null_when_job_has_no_recorded_ticks()
    {
        var registry = new InMemoryJobHealthRegistry();

        registry.Get("unknown").Should().BeNull();
    }

    [Fact]
    public void RecordSuccess_resets_consecutive_failures_and_stores_metrics()
    {
        var registry = new InMemoryJobHealthRegistry();
        var now = new DateTime(2026, 04, 22, 12, 0, 0, DateTimeKind.Utc);

        registry.RecordFailure("job-a", now, TimeSpan.FromMilliseconds(200), "boom", consecutiveFailures: 3);
        registry.RecordSuccess("job-a", now.AddMinutes(1), TimeSpan.FromMilliseconds(125));

        var snapshot = registry.Get("job-a");
        snapshot.Should().NotBeNull();
        snapshot!.ConsecutiveFailures.Should().Be(0);
        snapshot.LastSuccessUtc.Should().Be(now.AddMinutes(1));
        snapshot.LastSuccessDuration.Should().Be(TimeSpan.FromMilliseconds(125));
        snapshot.LastFailureUtc.Should().Be(now);
        snapshot.LastFailureMessage.Should().Be("boom");
    }

    [Fact]
    public void RecordNextTick_preserves_previous_success_and_failure_state()
    {
        var registry = new InMemoryJobHealthRegistry();
        var now = new DateTime(2026, 04, 22, 12, 0, 0, DateTimeKind.Utc);

        registry.RecordSuccess("job-b", now, TimeSpan.FromMilliseconds(50));
        registry.RecordNextTick("job-b", now.AddMinutes(15));

        var snapshot = registry.Get("job-b")!;
        snapshot.NextTickUtc.Should().Be(now.AddMinutes(15));
        snapshot.LastSuccessUtc.Should().Be(now);
    }

    [Fact]
    public void Snapshot_returns_isolated_copy_per_job()
    {
        var registry = new InMemoryJobHealthRegistry();
        var now = new DateTime(2026, 04, 22, 12, 0, 0, DateTimeKind.Utc);

        registry.RecordSuccess("job-a", now, TimeSpan.FromMilliseconds(10));
        registry.RecordFailure("job-b", now, TimeSpan.FromMilliseconds(20), "x", 1);

        var snapshot = registry.Snapshot();
        snapshot.Should().HaveCount(2);
        snapshot["job-a"].ConsecutiveFailures.Should().Be(0);
        snapshot["job-b"].ConsecutiveFailures.Should().Be(1);
    }
}
