using Microsoft.EntityFrameworkCore;
using TradingIntel.Application.Persistence;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;
using TradingIntel.Infrastructure.Persistence.Entities;

namespace TradingIntel.Infrastructure.Persistence.Repositories;

public sealed class SbcChallengeRepository : ISbcChallengeRepository
{
    private readonly TradingIntelDbContext _dbContext;

    public SbcChallengeRepository(TradingIntelDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task UpsertRangeAsync(IEnumerable<SbcChallenge> challenges, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(challenges);

        // Dedupe by Id preserving the last occurrence: a single tick can carry
        // repeated ids (FUT.GG listing + detail fallback), and EF would trip on
        // re-removing child rows already marked Added earlier in the batch.
        var incoming = challenges
            .GroupBy(c => c.Id)
            .Select(g => g.Last())
            .ToList();
        if (incoming.Count == 0)
        {
            return;
        }

        // Use List<Guid> (not Guid[]) so EF's expression funcletizer doesn't
        // pick the ReadOnlySpan<Guid> Contains overload, which breaks under
        // the .NET 9 expression interpreter on Linux (TRet can't be a ref
        // struct when compiling the lambda via FuncCallInstruction).
        var ids = incoming.Select(c => c.Id).ToList();

        // Load only the parent records (no Include). Requirements are treated
        // as a value list: we drop the whole child set server-side via
        // ExecuteDelete and reinsert, which avoids EF change-tracker pitfalls
        // when the same entities are touched twice in a single batch.
        var existing = await _dbContext.SbcChallenges
            .Where(c => ids.Contains(c.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var existingById = existing.ToDictionary(c => c.Id);

        if (existingById.Count > 0)
        {
            var existingIds = existingById.Keys.ToList();
            await _dbContext.SbcChallengeRequirements
                .Where(r => existingIds.Contains(r.ChallengeId))
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var challenge in incoming)
        {
            if (existingById.TryGetValue(challenge.Id, out var record))
            {
                record.Title = challenge.Title;
                record.Category = challenge.Category;
                record.ExpiresAtUtc = challenge.ExpiresAtUtc;
                record.RepeatabilityKind = challenge.Repeatability.Kind;
                record.RepeatabilityMaxCompletions = challenge.Repeatability.MaxCompletions;
                record.SetName = challenge.SetName;
                record.ObservedAtUtc = challenge.ObservedAtUtc;
            }
            else
            {
                record = new SbcChallengeRecord
                {
                    Id = challenge.Id,
                    Title = challenge.Title,
                    Category = challenge.Category,
                    ExpiresAtUtc = challenge.ExpiresAtUtc,
                    RepeatabilityKind = challenge.Repeatability.Kind,
                    RepeatabilityMaxCompletions = challenge.Repeatability.MaxCompletions,
                    SetName = challenge.SetName,
                    ObservedAtUtc = challenge.ObservedAtUtc,
                };
                _dbContext.SbcChallenges.Add(record);
            }

            foreach (var req in BuildRequirementRecords(challenge))
            {
                _dbContext.SbcChallengeRequirements.Add(req);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<SbcChallenge?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var record = await _dbContext.SbcChallenges
            .AsNoTracking()
            .Include(c => c.Requirements)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return record is null ? null : ToDomain(record);
    }

    public async Task<IReadOnlyList<SbcChallenge>> QueryAsync(
        SbcChallengeQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<SbcChallengeRecord> q = _dbContext.SbcChallenges
            .AsNoTracking()
            .Include(c => c.Requirements);

        if (query.ActiveAsOfUtc is { } nowUtc)
        {
            q = q.Where(c => c.ExpiresAtUtc == null || c.ExpiresAtUtc > nowUtc);
        }

        if (!string.IsNullOrWhiteSpace(query.CategoryContains))
        {
            var needle = query.CategoryContains;
            // SQLite LIKE is case-insensitive for ASCII by default.
            q = q.Where(c => EF.Functions.Like(c.Category, "%" + needle + "%"));
        }

        if (query.MatchesOverall is { } overall)
        {
            // List<string> (not string[]) for the same reason as the ids above:
            // avoids the ReadOnlySpan<string> Contains overload that breaks the
            // .NET 9 expression interpreter on Linux CI.
            var keys = SbcChallengeQuery.TeamRatingRequirementKeys
                .Select(k => k.ToLowerInvariant())
                .ToList();
            q = q.Where(c => c.Requirements.Any(r =>
                keys.Contains(r.Key.ToLower()) && r.Minimum <= overall));
        }

        var records = await q
            .OrderBy(c => c.ExpiresAtUtc == null ? 1 : 0)
            .ThenBy(c => c.ExpiresAtUtc)
            .ThenBy(c => c.Title)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return records.Select(ToDomain).ToList();
    }

    public async Task<(IReadOnlyList<SbcChallenge> Items, int TotalCount)> QueryActivePagedAsync(
        SbcActiveListQuery query,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.ActiveAsOfUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("ActiveAsOfUtc must be UTC.", nameof(query));
        }

        if (query.ExpiresBeforeUtc is { Kind: not DateTimeKind.Utc })
        {
            throw new ArgumentException("ExpiresBeforeUtc must be UTC when provided.", nameof(query));
        }

        IQueryable<SbcChallengeRecord> q = _dbContext.SbcChallenges
            .AsNoTracking()
            .Include(c => c.Requirements);

        if (!query.IncludeExpired)
        {
            q = q.Where(c => c.ExpiresAtUtc == null || c.ExpiresAtUtc > query.ActiveAsOfUtc);
        }

        if (query.ExpiresBeforeUtc is { } expiresBefore)
        {
            q = q.Where(c => c.ExpiresAtUtc != null && c.ExpiresAtUtc <= expiresBefore);
        }

        if (!string.IsNullOrWhiteSpace(query.CategoryContains))
        {
            var needle = query.CategoryContains;
            q = q.Where(c => EF.Functions.Like(c.Category, "%" + needle + "%"));
        }

        if (query.RequiresOverall is { } overall)
        {
            var keys = SbcChallengeQuery.TeamRatingRequirementKeys
                .Select(k => k.ToLowerInvariant())
                .ToList();
            q = q.Where(c => c.Requirements.Any(r =>
                keys.Contains(r.Key.ToLower()) && r.Minimum <= overall));
        }

        q = q
            .OrderBy(c => c.ExpiresAtUtc == null ? 1 : 0)
            .ThenBy(c => c.ExpiresAtUtc)
            .ThenBy(c => c.Title);

        var totalCount = await q.CountAsync(cancellationToken).ConfigureAwait(false);
        var records = await q
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (records.Select(ToDomain).ToList(), totalCount);
    }

    private static IEnumerable<SbcChallengeRequirementRecord> BuildRequirementRecords(SbcChallenge challenge)
    {
        return challenge.Requirements.Select((req, index) => new SbcChallengeRequirementRecord
        {
            Id = Guid.NewGuid(),
            ChallengeId = challenge.Id,
            Key = req.Key,
            Minimum = req.Minimum,
            Maximum = req.Maximum,
            Order = index,
        });
    }

    private static SbcChallenge ToDomain(SbcChallengeRecord record)
    {
        var repeatability = record.RepeatabilityKind switch
        {
            SbcRepeatabilityKind.NotRepeatable => SbcRepeatability.NotRepeatable(),
            SbcRepeatabilityKind.Limited => SbcRepeatability.Limited(record.RepeatabilityMaxCompletions ?? 1),
            SbcRepeatabilityKind.Unlimited => SbcRepeatability.Unlimited(),
            _ => SbcRepeatability.Unknown(),
        };

        var requirements = record.Requirements
            .OrderBy(r => r.Order)
            .Select(r => new SbcRequirement(r.Key, r.Minimum, r.Maximum))
            .ToArray();

        return new SbcChallenge(
            id: record.Id,
            title: record.Title,
            category: record.Category,
            expiresAtUtc: record.ExpiresAtUtc is null
                ? null
                : DateTime.SpecifyKind(record.ExpiresAtUtc.Value, DateTimeKind.Utc),
            repeatability: repeatability,
            setName: record.SetName,
            observedAtUtc: DateTime.SpecifyKind(record.ObservedAtUtc, DateTimeKind.Utc),
            requirements: requirements);
    }
}
