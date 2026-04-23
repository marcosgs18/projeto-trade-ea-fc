using Microsoft.Extensions.Logging;
using TradingIntel.Application.Sbc;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;

namespace TradingIntel.Application.Trading;

/// <summary>
/// Implementação V1 do <see cref="ITradeScoringService"/>: fórmula linear, sem ML, explicável.
/// </summary>
/// <remarks>
/// A lógica é pura (sem I/O). Dado o mesmo <see cref="TradeScoringInput"/>, o resultado é
/// determinístico — facilitando testes, replays e auditoria.
/// </remarks>
public sealed class TradeScoringService : ITradeScoringService
{
    private readonly ILogger<TradeScoringService> _logger;

    public TradeScoringService(ILogger<TradeScoringService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public TradeOpportunity? Score(TradeScoringInput input, TradeScoringWeights? weights = null)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));
        var w = weights ?? TradeScoringWeights.Default;

        var (buyPrice, sellPrice) = ResolveSuggestedPrices(input.CurrentPrice);

        var netSell = Math.Floor(sellPrice * (1m - TradeOpportunity.EaMarketTaxRate));
        var netMargin = netSell - buyPrice;

        if (netMargin <= 0 || sellPrice <= buyPrice)
        {
            _logger.LogInformation(
                "TradeScoring: no net edge. player={Player} overall={Overall} buy={Buy} sell={Sell} netMargin={NetMargin}",
                input.Player, input.OverallRating, buyPrice, sellPrice, netMargin);
            return null;
        }

        var demandFactor = ComputeDemandFactor(input);
        var spreadFactor = ComputeSpreadFactor(buyPrice, netMargin, w);
        var liquidityFactor = ComputeLiquidityFactor(input, w);
        var stabilityFactor = ComputeStabilityFactor(input, w);
        var urgencyFactor = ComputeUrgencyFactor(input.NearestSbcExpiryUtc, input.NowUtc);

        var raw =
            w.Demand * demandFactor
            + w.Spread * spreadFactor
            + w.Liquidity * liquidityFactor
            + w.Stability * stabilityFactor
            + w.Urgency * urgencyFactor;

        var confidence = ClampUnit(raw);

        var reasons = BuildReasons(input, buyPrice, sellPrice, netMargin, netSell,
            demandFactor, spreadFactor, liquidityFactor, stabilityFactor, urgencyFactor, confidence);

        var opportunityId = Guid.NewGuid();
        var suggestions = new[]
        {
            new ExecutionSuggestion(
                id: Guid.NewGuid(),
                opportunityId: opportunityId,
                action: ExecutionAction.Buy,
                targetPrice: new Coins(buyPrice),
                validUntilUtc: input.NowUtc.AddMinutes(15)),
            new ExecutionSuggestion(
                id: Guid.NewGuid(),
                opportunityId: opportunityId,
                action: ExecutionAction.ListForSale,
                targetPrice: new Coins(sellPrice),
                validUntilUtc: input.NowUtc.AddHours(24)),
        };

        var opportunity = new TradeOpportunity(
            id: opportunityId,
            player: input.Player,
            detectedAtUtc: input.NowUtc,
            expectedBuyPrice: new Coins(buyPrice),
            expectedSellPrice: new Coins(sellPrice),
            confidence: new ConfidenceScore((decimal)Math.Round(confidence, 4)),
            reasons: reasons,
            suggestions: suggestions);

        _logger.LogInformation(
            "TradeScoring: opportunity detected. player={Player} overall={Overall} confidence={Confidence:0.###} " +
            "buy={Buy} sell={Sell} netMargin={NetMargin} factors=[demand={D:0.##} spread={S:0.##} liq={L:0.##} stab={St:0.##} urg={U:0.##}]",
            input.Player, input.OverallRating, confidence, buyPrice, sellPrice, netMargin,
            demandFactor, spreadFactor, liquidityFactor, stabilityFactor, urgencyFactor);

        return opportunity;
    }

    // ---------- suggested prices ----------

    private static (decimal Buy, decimal Sell) ResolveSuggestedPrices(PlayerPriceSnapshot price)
    {
        var buy = price.BuyNowPrice.Value;
        var sell = price.SellNowPrice?.Value ?? price.MedianMarketPrice.Value;
        // Se sell < buy por qualquer inconsistência de snapshot, puxa sell para a mediana.
        if (sell < buy)
        {
            sell = Math.Max(price.MedianMarketPrice.Value, buy);
        }

        return (buy, sell);
    }

    // ---------- factor calculations ----------

    private static double ComputeDemandFactor(TradeScoringInput input)
    {
        double best = 0;
        foreach (var band in input.DemandByBand)
        {
            if (input.OverallRating >= band.Band.FromRating
                && input.OverallRating <= band.Band.ToRating)
            {
                if (band.Score > best)
                {
                    best = band.Score;
                }
            }
        }

        return ClampUnit(best);
    }

    private static double ComputeSpreadFactor(decimal buyPrice, decimal netMargin, TradeScoringWeights w)
    {
        if (buyPrice <= 0) return 0;
        var rel = (double)(netMargin / buyPrice);
        return ClampUnit(rel / w.SpreadSaturation);
    }

    private static double ComputeLiquidityFactor(TradeScoringInput input, TradeScoringWeights w)
    {
        var count = 0;
        foreach (var listing in input.RecentListings)
        {
            if (listing.CapturedAtUtc <= input.NowUtc)
            {
                count++;
            }
        }

        return ClampUnit((double)count / w.LiquiditySaturation);
    }

    private static double ComputeStabilityFactor(TradeScoringInput input, TradeScoringWeights w)
    {
        var cutoff = input.NowUtc - w.VolatilityWindow;
        var points = new List<double>();
        foreach (var snap in input.PriceHistory)
        {
            if (snap.CapturedAtUtc >= cutoff && snap.CapturedAtUtc <= input.NowUtc)
            {
                points.Add((double)snap.BuyNowPrice.Value);
            }
        }

        if (points.Count < 2)
        {
            return 0.5;
        }

        double mean = 0;
        for (var i = 0; i < points.Count; i++) mean += points[i];
        mean /= points.Count;

        if (mean <= 0) return 0.5;

        double sumSq = 0;
        for (var i = 0; i < points.Count; i++)
        {
            var d = points[i] - mean;
            sumSq += d * d;
        }
        var variance = sumSq / points.Count;
        var stdDev = Math.Sqrt(variance);
        var cv = stdDev / mean;

        return ClampUnit(1.0 - (cv / w.VolatilitySaturation));
    }

    internal static double ComputeUrgencyFactor(DateTime? expiresAtUtc, DateTime nowUtc)
    {
        if (expiresAtUtc is null) return 0.5;

        var delta = expiresAtUtc.Value - nowUtc;
        if (delta <= TimeSpan.Zero) return 0.0;
        if (delta <= TimeSpan.FromHours(24)) return 1.0;
        if (delta <= TimeSpan.FromHours(72)) return 0.75;
        if (delta <= TimeSpan.FromDays(7)) return 0.6;
        return 0.45;
    }

    // ---------- reasons ----------

    private static IReadOnlyList<OpportunityReason> BuildReasons(
        TradeScoringInput input,
        decimal buyPrice,
        decimal sellPrice,
        decimal netMargin,
        decimal netSell,
        double demandFactor,
        double spreadFactor,
        double liquidityFactor,
        double stabilityFactor,
        double urgencyFactor,
        double confidence)
    {
        var relSpreadPct = buyPrice > 0 ? (double)(netMargin / buyPrice) * 100.0 : 0.0;
        var listingCount = input.RecentListings.Count(l => l.CapturedAtUtc <= input.NowUtc);

        var reasons = new List<OpportunityReason>
        {
            new OpportunityReason(
                code: "TRADE_SCORE",
                message:
                    $"Score {confidence:0.###} para {input.Player.DisplayName} (overall {input.OverallRating}); " +
                    $"compra sugerida {buyPrice:0} / venda {sellPrice:0} → líquido {netSell:0} " +
                    $"(margem {netMargin:0}, {relSpreadPct:0.#}%).",
                weight: (decimal)Math.Round(confidence, 4)),
            new OpportunityReason(
                code: "DEMAND_OVERALL",
                message: BuildDemandMessage(input, demandFactor),
                weight: (decimal)Math.Round(demandFactor, 4)),
            new OpportunityReason(
                code: "NET_SPREAD",
                message:
                    $"Spread líquido após taxa de 5%: {netMargin:0} coins ({relSpreadPct:0.#}% sobre compra).",
                weight: (decimal)Math.Round(spreadFactor, 4)),
            new OpportunityReason(
                code: "MARKET_LIQUIDITY",
                message: $"{listingCount} listagem(ns) recente(s) na amostra.",
                weight: (decimal)Math.Round(liquidityFactor, 4)),
            new OpportunityReason(
                code: "PRICE_STABILITY",
                message: BuildStabilityMessage(input, stabilityFactor),
                weight: (decimal)Math.Round(stabilityFactor, 4)),
            new OpportunityReason(
                code: "SBC_EXPIRY_WINDOW",
                message: BuildUrgencyMessage(input),
                weight: (decimal)Math.Round(urgencyFactor, 4)),
        };

        return reasons;
    }

    private static string BuildDemandMessage(TradeScoringInput input, double demandFactor)
    {
        RatingBandDemandScore? match = null;
        foreach (var band in input.DemandByBand)
        {
            if (input.OverallRating >= band.Band.FromRating
                && input.OverallRating <= band.Band.ToRating)
            {
                if (match is null || band.Score > match.Score)
                {
                    match = band;
                }
            }
        }

        if (match is null)
        {
            return $"Sem SBCs ativos pedindo overall {input.OverallRating}.";
        }

        return $"Faixa {match.Band} com {match.TotalRequiredCards} carta(s) demandada(s) por " +
               $"{match.ContributingChallengeIds.Count} SBC(s) — score de demanda {match.Score:0.##} " +
               $"(fator {demandFactor:0.##}).";
    }

    private static string BuildStabilityMessage(TradeScoringInput input, double stability)
    {
        var points = input.PriceHistory.Count(p =>
            p.CapturedAtUtc >= input.NowUtc - TimeSpan.FromDays(1)
            && p.CapturedAtUtc <= input.NowUtc);

        if (points < 2)
        {
            return $"Histórico insuficiente ({points} ponto(s)); estabilidade neutra.";
        }

        return $"Estabilidade {stability:0.##} com base em {points} snapshot(s) recentes.";
    }

    private static string BuildUrgencyMessage(TradeScoringInput input)
    {
        if (input.NearestSbcExpiryUtc is null)
        {
            return "Sem SBC com expiração conhecida; urgência neutra.";
        }

        var delta = input.NearestSbcExpiryUtc.Value - input.NowUtc;
        if (delta <= TimeSpan.Zero)
        {
            return "SBC mais próximo já expirou; sem janela.";
        }

        var label = delta switch
        {
            var d when d <= TimeSpan.FromHours(24) => "≤ 24h (janela apertada)",
            var d when d <= TimeSpan.FromHours(72) => "≤ 72h",
            var d when d <= TimeSpan.FromDays(7) => "≤ 7 dias",
            _ => "> 7 dias",
        };

        return $"SBC mais próximo expira em {label}.";
    }

    // ---------- helpers ----------

    private static double ClampUnit(double value)
    {
        if (double.IsNaN(value)) return 0;
        if (value < 0) return 0;
        if (value > 1) return 1;
        return value;
    }
}
