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

    private sealed record ReasonDto(string Code, string Message, decimal Weight);

    private sealed record SuggestionDto(
        Guid Id,
        Guid OpportunityId,
        int Action,
        decimal TargetPrice,
        DateTime ValidUntilUtc);
}
