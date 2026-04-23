using TradingIntel.Application.Persistence;
using TradingIntel.Domain.Models;

namespace TradingIntel.Tests.Worker.Fakes;

/// <summary>
/// In-memory fake for <see cref="ISbcChallengeRepository"/> used by job tests.
/// Captures every batch received on <see cref="UpsertRangeAsync"/> so tests can
/// assert both "what was persisted" and "how many times the job wrote".
/// Reproduces the production upsert-by-id behaviour so idempotency assertions
/// against this fake match what the SQLite repo does.
/// </summary>
internal sealed class CapturingSbcChallengeRepository : ISbcChallengeRepository
{
    private readonly Dictionary<Guid, SbcChallenge> _state = new();

    public List<IReadOnlyList<SbcChallenge>> UpsertCalls { get; } = new();

    public IReadOnlyDictionary<Guid, SbcChallenge> State => _state;

    public Task UpsertRangeAsync(IEnumerable<SbcChallenge> challenges, CancellationToken cancellationToken)
    {
        var batch = challenges.ToList();
        UpsertCalls.Add(batch);
        foreach (var challenge in batch)
        {
            _state[challenge.Id] = challenge;
        }

        return Task.CompletedTask;
    }

    public Task<SbcChallenge?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        _state.TryGetValue(id, out var challenge);
        return Task.FromResult(challenge);
    }

    public Task<IReadOnlyList<SbcChallenge>> QueryAsync(SbcChallengeQuery query, CancellationToken cancellationToken)
    {
        IEnumerable<SbcChallenge> results = _state.Values;

        if (query.ActiveAsOfUtc is { } nowUtc)
        {
            results = results.Where(c => c.ExpiresAtUtc is null || c.ExpiresAtUtc > nowUtc);
        }

        if (!string.IsNullOrWhiteSpace(query.CategoryContains))
        {
            results = results.Where(c =>
                c.Category.Contains(query.CategoryContains, StringComparison.OrdinalIgnoreCase));
        }

        if (query.MatchesOverall is { } overall)
        {
            var keys = new HashSet<string>(SbcChallengeQuery.TeamRatingRequirementKeys, StringComparer.OrdinalIgnoreCase);
            results = results.Where(c => c.Requirements.Any(r => keys.Contains(r.Key) && r.Minimum <= overall));
        }

        return Task.FromResult<IReadOnlyList<SbcChallenge>>(results.ToList());
    }
}
