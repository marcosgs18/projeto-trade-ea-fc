using TradingIntel.Application.Sbc;
using TradingIntel.Domain.Models;
using TradingIntel.Domain.ValueObjects;

namespace TradingIntel.Application.Trading;

/// <summary>
/// Pacote de entrada do <see cref="ITradeScoringService"/>.
/// Todos os valores derivados (volatilidade, liquidez, demanda por overall) são calculados
/// a partir desta estrutura — o serviço é puro e determinístico.
/// </summary>
public sealed record TradeScoringInput
{
    public TradeScoringInput(
        PlayerReference player,
        int overallRating,
        PlayerPriceSnapshot currentPrice,
        IReadOnlyList<PlayerPriceSnapshot> priceHistory,
        IReadOnlyList<MarketListingSnapshot> recentListings,
        IReadOnlyList<RatingBandDemandScore> demandByBand,
        DateTime nowUtc,
        DateTime? nearestSbcExpiryUtc = null)
    {
        if (overallRating is < 0 or > 99)
        {
            throw new ArgumentOutOfRangeException(nameof(overallRating), "overallRating must be between 0 and 99.");
        }

        Player = player;
        OverallRating = overallRating;
        CurrentPrice = currentPrice ?? throw new ArgumentNullException(nameof(currentPrice));
        PriceHistory = priceHistory ?? throw new ArgumentNullException(nameof(priceHistory));
        RecentListings = recentListings ?? throw new ArgumentNullException(nameof(recentListings));
        DemandByBand = demandByBand ?? throw new ArgumentNullException(nameof(demandByBand));
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("nowUtc must be UTC.", nameof(nowUtc));
        }
        NowUtc = nowUtc;

        if (nearestSbcExpiryUtc is not null)
        {
            if (nearestSbcExpiryUtc.Value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException("nearestSbcExpiryUtc must be UTC.", nameof(nearestSbcExpiryUtc));
            }

            NearestSbcExpiryUtc = nearestSbcExpiryUtc.Value;
        }

        if (currentPrice.Player.PlayerId != player.PlayerId)
        {
            throw new ArgumentException("currentPrice.Player must match player.", nameof(currentPrice));
        }
    }

    public PlayerReference Player { get; }

    /// <summary>Overall da carta do jogador (0-99).</summary>
    public int OverallRating { get; }

    /// <summary>Snapshot de preço atual usado como referência de compra/venda.</summary>
    public PlayerPriceSnapshot CurrentPrice { get; }

    /// <summary>Histórico de preços para cálculo de volatilidade (ordem livre).</summary>
    public IReadOnlyList<PlayerPriceSnapshot> PriceHistory { get; }

    /// <summary>Listagens recentes usadas como proxy de liquidez.</summary>
    public IReadOnlyList<MarketListingSnapshot> RecentListings { get; }

    /// <summary>
    /// Scores de demanda por faixa de overall (tipicamente produzidos por
    /// <see cref="IRatingBandDemandService"/>). O serviço escolhe a faixa que contém
    /// <see cref="OverallRating"/>.
    /// </summary>
    public IReadOnlyList<RatingBandDemandScore> DemandByBand { get; }

    /// <summary>Momento de referência (UTC) usado para urgência e volatilidade.</summary>
    public DateTime NowUtc { get; }

    /// <summary>
    /// Expiração do SBC ativo mais próximo que demanda a faixa deste overall.
    /// Quando ausente, a urgência fica neutra (0,5).
    /// </summary>
    public DateTime? NearestSbcExpiryUtc { get; }
}
