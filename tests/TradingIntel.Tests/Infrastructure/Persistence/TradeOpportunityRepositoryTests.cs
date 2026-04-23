using FluentAssertions;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;
using TradingIntel.Infrastructure.Persistence.Repositories;
using Xunit;

namespace TradingIntel.Tests.Infrastructure.Persistence;

public sealed class TradeOpportunityRepositoryTests : IClassFixture<PersistenceTestFixture>
{
    private readonly PersistenceTestFixture _fixture;

    public TradeOpportunityRepositoryTests(PersistenceTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Upsert_exists_delete_roundtrip()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new TradeOpportunityRepository(ctx);
        var playerId = 50_000 + Random.Shared.Next(1, 9000);
        var player = new PlayerReference(playerId, "Repo Test");
        var oppId = Guid.NewGuid();
        var now = DateTime.UtcNow;
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

        (await repo.ExistsForPlayerAsync(playerId, CancellationToken.None)).Should().BeFalse();

        await repo.UpsertAsync(opportunity, now, CancellationToken.None);

        (await repo.ExistsForPlayerAsync(playerId, CancellationToken.None)).Should().BeTrue();

        await repo.DeleteByPlayerIdAsync(playerId, CancellationToken.None);

        (await repo.ExistsForPlayerAsync(playerId, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task MarkStaleWhereLastRecomputedBeforeAsync_marks_only_old_rows()
    {
        await using var ctx = _fixture.CreateContext();
        var repo = new TradeOpportunityRepository(ctx);
        var playerId = 60_000 + Random.Shared.Next(1, 9000);
        var player = new PlayerReference(playerId, "Stale Test");
        var oppId = Guid.NewGuid();
        var old = DateTime.UtcNow.AddHours(-2);
        var opportunity = new TradeOpportunity(
            oppId,
            player,
            old,
            new Coins(10_000),
            new Coins(12_000),
            new ConfidenceScore(0.5m),
            new[] { new OpportunityReason("TRADE_SCORE", "ok", 0.5m) },
            new[]
            {
                new ExecutionSuggestion(Guid.NewGuid(), oppId, ExecutionAction.Buy, new Coins(10_000), old.AddMinutes(15)),
                new ExecutionSuggestion(Guid.NewGuid(), oppId, ExecutionAction.ListForSale, new Coins(12_000), old.AddHours(24)),
            });

        await repo.UpsertAsync(opportunity, old, CancellationToken.None);

        var affected = await repo.MarkStaleWhereLastRecomputedBeforeAsync(
            DateTime.UtcNow.AddMinutes(-30),
            CancellationToken.None);

        affected.Should().Be(1);
    }
}
