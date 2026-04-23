namespace TradingIntel.Application.Sbc;

/// <summary>
/// Pesos configuráveis usados pelo <see cref="IRatingBandDemandService"/> para compor o score
/// final de uma faixa de rating. Os quatro pesos principais devem somar 1,0.
/// </summary>
public sealed record RatingBandDemandWeights
{
    /// <summary>Configuração padrão, sintonizada para cenários com poucos SBCs simultâneos por faixa.</summary>
    public static RatingBandDemandWeights Default { get; } = new();

    public RatingBandDemandWeights(
        double cardVolume = 0.45,
        double repeatability = 0.25,
        double expiryUrgency = 0.15,
        double category = 0.15,
        int cardVolumeSaturation = 22,
        int challengeSaturation = 3,
        int maxReasonsPerBand = 6)
    {
        ValidateWeight(cardVolume, nameof(cardVolume));
        ValidateWeight(repeatability, nameof(repeatability));
        ValidateWeight(expiryUrgency, nameof(expiryUrgency));
        ValidateWeight(category, nameof(category));

        var sum = cardVolume + repeatability + expiryUrgency + category;
        if (Math.Abs(sum - 1.0) > 1e-6)
        {
            throw new ArgumentException(
                $"Os pesos cardVolume+repeatability+expiryUrgency+category devem somar 1,0 (atual: {sum:0.###}).",
                nameof(cardVolume));
        }

        if (cardVolumeSaturation <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cardVolumeSaturation), "cardVolumeSaturation deve ser > 0.");
        }

        if (challengeSaturation <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(challengeSaturation), "challengeSaturation deve ser > 0.");
        }

        if (maxReasonsPerBand <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxReasonsPerBand), "maxReasonsPerBand deve ser > 0.");
        }

        CardVolume = cardVolume;
        Repeatability = repeatability;
        ExpiryUrgency = expiryUrgency;
        Category = category;
        CardVolumeSaturation = cardVolumeSaturation;
        ChallengeSaturation = challengeSaturation;
        MaxReasonsPerBand = maxReasonsPerBand;
    }

    /// <summary>Peso do volume bruto de cartas requisitadas na faixa.</summary>
    public double CardVolume { get; }

    /// <summary>Peso da repetibilidade (ilimitado &gt; limitado &gt; único).</summary>
    public double Repeatability { get; }

    /// <summary>Peso da urgência (proximidade da expiração).</summary>
    public double ExpiryUrgency { get; }

    /// <summary>Peso da categoria do SBC (upgrades costumam ser mais recorrentes).</summary>
    public double Category { get; }

    /// <summary>
    /// Quantidade de cartas que satura o fator de volume em 1,0 (ex.: 22 ≈ 2 squads completos).
    /// </summary>
    public int CardVolumeSaturation { get; }

    /// <summary>
    /// Quantidade de contribuições de SBC que satura o score agregado em 1,0 quando cada contribuição é máxima.
    /// </summary>
    public int ChallengeSaturation { get; }

    /// <summary>Limite superior de <see cref="DemandReason"/> emitidos por faixa, ordenados por relevância.</summary>
    public int MaxReasonsPerBand { get; }

    private static void ValidateWeight(double value, string paramName)
    {
        if (double.IsNaN(value) || value < 0 || value > 1)
        {
            throw new ArgumentOutOfRangeException(paramName, "weight must be between 0 and 1.");
        }
    }
}
