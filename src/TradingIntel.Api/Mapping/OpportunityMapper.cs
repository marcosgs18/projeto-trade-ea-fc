using TradingIntel.Api.Contracts;
using TradingIntel.Application.Persistence;
using TradingIntel.Application.Trading;
using TradingIntel.Domain.Models;

namespace TradingIntel.Api.Mapping;

internal static class OpportunityMapper
{
    public static OpportunitySummaryResponse ToSummary(TradeOpportunityStoredView view)
    {
        var o = view.Opportunity;
        return new OpportunitySummaryResponse(
            o.Id,
            o.Player.PlayerId,
            o.Player.DisplayName,
            o.DetectedAtUtc,
            o.ExpectedBuyPrice.Value,
            o.ExpectedSellPrice.Value,
            o.ExpectedNetMargin.Value,
            o.Confidence.Value,
            view.IsStale,
            view.LastRecomputedAtUtc);
    }

    public static OpportunityDetailResponse ToDetail(TradeOpportunityStoredView view)
    {
        var o = view.Opportunity;
        var reasons = o.Reasons
            .Select(r => new OpportunityReasonResponse(r.Code, r.Message, r.Weight))
            .ToList();
        var suggestions = o.Suggestions
            .Select(s => new ExecutionSuggestionResponse(
                s.Id,
                s.OpportunityId,
                s.Action.ToString(),
                s.TargetPrice.Value,
                s.ValidUntilUtc))
            .ToList();

        return new OpportunityDetailResponse(
            o.Id,
            o.Player.PlayerId,
            o.Player.DisplayName,
            o.DetectedAtUtc,
            o.ExpectedBuyPrice.Value,
            o.ExpectedSellPrice.Value,
            o.ExpectedProfit.Value,
            o.ExpectedNetMargin.Value,
            o.Confidence.Value,
            view.IsStale,
            view.LastRecomputedAtUtc,
            reasons,
            suggestions);
    }

    public static OpportunityRecomputeResponse ToResponse(OpportunityRecomputeSummary s) =>
        new(s.Upserted, s.RemovedNoEdge, s.SkippedMissingOverall, s.SkippedMissingPrice, s.StaleMarked);
}
