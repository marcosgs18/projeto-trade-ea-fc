using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TradingIntel.Application.Persistence;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;
using TradingIntel.Infrastructure.Persistence.Entities;

namespace TradingIntel.Infrastructure.Persistence.Repositories;

public sealed class TradeOpportunityRepository : ITradeOpportunityRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly TradingIntelDbContext _dbContext;

    public TradeOpportunityRepository(TradingIntelDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task UpsertAsync(
        TradeOpportunity opportunity,
        DateTime lastRecomputedAtUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(opportunity);
        if (lastRecomputedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("lastRecomputedAtUtc must be UTC.", nameof(lastRecomputedAtUtc));
        }

        var reasonsJson = JsonSerializer.Serialize(
            opportunity.Reasons.Select(r => new ReasonDto(r.Code, r.Message, r.Weight)).ToList(),
            JsonOptions);

        var suggestionsJson = JsonSerializer.Serialize(
            opportunity.Suggestions.Select(s => new SuggestionDto(
                    s.Id,
                    s.OpportunityId,
                    (int)s.Action,
                    s.TargetPrice.Value,
                    s.ValidUntilUtc))
                .ToList(),
            JsonOptions);

        var playerId = opportunity.Player.PlayerId;
        var existing = await _dbContext.TradeOpportunities
            .FirstOrDefaultAsync(e => e.PlayerId == playerId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            _dbContext.TradeOpportunities.Add(new TradeOpportunityRecord
            {
                PlayerId = playerId,
                OpportunityId = opportunity.Id,
                PlayerDisplayName = opportunity.Player.DisplayName,
                DetectedAtUtc = opportunity.DetectedAtUtc,
                ExpectedBuyPrice = opportunity.ExpectedBuyPrice.Value,
                ExpectedSellPrice = opportunity.ExpectedSellPrice.Value,
                ExpectedNetMargin = opportunity.ExpectedNetMargin.Value,
                Confidence = opportunity.Confidence.Value,
                ReasonsJson = reasonsJson,
                SuggestionsJson = suggestionsJson,
                LastRecomputedAtUtc = lastRecomputedAtUtc,
                IsStale = false,
            });
        }
        else
        {
            existing.OpportunityId = opportunity.Id;
            existing.PlayerDisplayName = opportunity.Player.DisplayName;
            existing.DetectedAtUtc = opportunity.DetectedAtUtc;
            existing.ExpectedBuyPrice = opportunity.ExpectedBuyPrice.Value;
            existing.ExpectedSellPrice = opportunity.ExpectedSellPrice.Value;
            existing.ExpectedNetMargin = opportunity.ExpectedNetMargin.Value;
            existing.Confidence = opportunity.Confidence.Value;
            existing.ReasonsJson = reasonsJson;
            existing.SuggestionsJson = suggestionsJson;
            existing.LastRecomputedAtUtc = lastRecomputedAtUtc;
            existing.IsStale = false;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteByPlayerIdAsync(long playerId, CancellationToken cancellationToken)
    {
        await _dbContext.TradeOpportunities
            .Where(e => e.PlayerId == playerId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> MarkStaleWhereLastRecomputedBeforeAsync(
        DateTime cutoffUtc,
        CancellationToken cancellationToken)
    {
        if (cutoffUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("cutoffUtc must be UTC.", nameof(cutoffUtc));
        }

        return await _dbContext.TradeOpportunities
            .Where(e => !e.IsStale && e.LastRecomputedAtUtc < cutoffUtc)
            .ExecuteUpdateAsync(
                s => s.SetProperty(e => e.IsStale, true),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExistsForPlayerAsync(long playerId, CancellationToken cancellationToken)
    {
        return await _dbContext.TradeOpportunities
            .AsNoTracking()
            .AnyAsync(e => e.PlayerId == playerId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<TradeOpportunityStoredView> Items, int TotalCount)> QueryPagedAsync(
        TradeOpportunityListFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (filter.DetectedAfterUtc is { Kind: not DateTimeKind.Utc })
        {
            throw new ArgumentException("DetectedAfterUtc must be UTC when provided.", nameof(filter));
        }

        var query = _dbContext.TradeOpportunities.AsNoTracking();

        if (filter.MinConfidence is { } minConf)
        {
            query = query.Where(e => e.Confidence >= minConf);
        }

        if (filter.MinNetMargin is { } minNet)
        {
            query = query.Where(e => e.ExpectedNetMargin >= minNet);
        }

        if (filter.PlayerId is { } playerId)
        {
            query = query.Where(e => e.PlayerId == playerId);
        }

        if (filter.DetectedAfterUtc is { } detectedAfter)
        {
            query = query.Where(e => e.DetectedAtUtc >= detectedAfter);
        }

        query = query.OrderByDescending(e => e.DetectedAtUtc);

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var records = await query
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (
            records
                .Select(r => new TradeOpportunityStoredView(ToDomain(r), r.LastRecomputedAtUtc, r.IsStale))
                .ToList(),
            totalCount);
    }

    public async Task<TradeOpportunityStoredView?> GetByOpportunityIdAsync(
        Guid opportunityId,
        CancellationToken cancellationToken)
    {
        var record = await _dbContext.TradeOpportunities
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.OpportunityId == opportunityId, cancellationToken)
            .ConfigureAwait(false);

        return record is null
            ? null
            : new TradeOpportunityStoredView(ToDomain(record), record.LastRecomputedAtUtc, record.IsStale);
    }

    private static TradeOpportunity ToDomain(TradeOpportunityRecord record)
    {
        var reasonDtos = JsonSerializer.Deserialize<List<ReasonDto>>(record.ReasonsJson, JsonOptions)
            ?? throw new InvalidOperationException("ReasonsJson is invalid.");
        var suggestionDtos = JsonSerializer.Deserialize<List<SuggestionDto>>(record.SuggestionsJson, JsonOptions)
            ?? throw new InvalidOperationException("SuggestionsJson is invalid.");

        var reasons = reasonDtos
            .Select(d => new OpportunityReason(d.Code, d.Message, d.Weight))
            .ToArray();

        var player = new PlayerReference(record.PlayerId, record.PlayerDisplayName);
        var detectedAt = DateTime.SpecifyKind(record.DetectedAtUtc, DateTimeKind.Utc);

        var suggestions = suggestionDtos
            .Select(d =>
            {
                if (!Enum.IsDefined(typeof(ExecutionAction), d.Action))
                {
                    throw new InvalidOperationException($"Unknown execution action: {d.Action}.");
                }

                var action = (ExecutionAction)d.Action;
                var validUntil = DateTime.SpecifyKind(d.ValidUntilUtc, DateTimeKind.Utc);
                return new ExecutionSuggestion(d.Id, d.OpportunityId, action, new Coins(d.TargetPrice), validUntil);
            })
            .ToArray();

        return new TradeOpportunity(
            record.OpportunityId,
            player,
            detectedAt,
            new Coins(record.ExpectedBuyPrice),
            new Coins(record.ExpectedSellPrice),
            new ConfidenceScore(record.Confidence),
            reasons,
            suggestions);
    }

    private sealed record ReasonDto(string Code, string Message, decimal Weight);

    private sealed record SuggestionDto(
        Guid Id,
        Guid OpportunityId,
        int Action,
        decimal TargetPrice,
        DateTime ValidUntilUtc);
}
