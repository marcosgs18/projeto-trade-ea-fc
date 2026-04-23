namespace TradingIntel.Application.Trading;

/// <summary>
/// Pesos e parâmetros de saturação da fórmula do <see cref="ITradeScoringService"/>.
/// Os cinco pesos principais devem somar 1,0 — isso mantém o score final dentro de <c>[0, 1]</c>
/// sem precisar de normalização posterior e facilita comparar execuções.
/// </summary>
public sealed record TradeScoringWeights
{
    /// <summary>Configuração padrão para V1.</summary>
    public static TradeScoringWeights Default { get; } = new();

    public TradeScoringWeights(
        double demand = 0.30,
        double spread = 0.30,
        double liquidity = 0.15,
        double stability = 0.10,
        double urgency = 0.15,
        double spreadSaturation = 0.20,
        int liquiditySaturation = 25,
        double volatilitySaturation = 0.25,
        TimeSpan? volatilityWindow = null)
    {
        ValidateUnit(demand, nameof(demand));
        ValidateUnit(spread, nameof(spread));
        ValidateUnit(liquidity, nameof(liquidity));
        ValidateUnit(stability, nameof(stability));
        ValidateUnit(urgency, nameof(urgency));

        var sum = demand + spread + liquidity + stability + urgency;
        if (Math.Abs(sum - 1.0) > 1e-6)
        {
            throw new ArgumentException(
                $"Os pesos demand+spread+liquidity+stability+urgency devem somar 1,0 (atual: {sum:0.###}).",
                nameof(demand));
        }

        if (spreadSaturation <= 0 || double.IsNaN(spreadSaturation))
        {
            throw new ArgumentOutOfRangeException(nameof(spreadSaturation), "spreadSaturation deve ser > 0.");
        }

        if (liquiditySaturation <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(liquiditySaturation), "liquiditySaturation deve ser > 0.");
        }

        if (volatilitySaturation <= 0 || double.IsNaN(volatilitySaturation))
        {
            throw new ArgumentOutOfRangeException(nameof(volatilitySaturation), "volatilitySaturation deve ser > 0.");
        }

        var window = volatilityWindow ?? TimeSpan.FromHours(24);
        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(volatilityWindow), "volatilityWindow deve ser > 0.");
        }

        Demand = demand;
        Spread = spread;
        Liquidity = liquidity;
        Stability = stability;
        Urgency = urgency;
        SpreadSaturation = spreadSaturation;
        LiquiditySaturation = liquiditySaturation;
        VolatilitySaturation = volatilitySaturation;
        VolatilityWindow = window;
    }

    /// <summary>Peso da demanda agregada (score de overall do jogador).</summary>
    public double Demand { get; }

    /// <summary>Peso do spread líquido esperado após taxa de 5%.</summary>
    public double Spread { get; }

    /// <summary>Peso da liquidez observada (número de listagens recentes).</summary>
    public double Liquidity { get; }

    /// <summary>Peso da estabilidade (inverso da volatilidade recente).</summary>
    public double Stability { get; }

    /// <summary>Peso da janela de oportunidade por expiração de SBC.</summary>
    public double Urgency { get; }

    /// <summary>Spread líquido relativo que satura o fator em 1,0 (ex.: <c>0,20</c> = 20%).</summary>
    public double SpreadSaturation { get; }

    /// <summary>Quantidade de listagens recentes que satura o fator de liquidez em 1,0.</summary>
    public int LiquiditySaturation { get; }

    /// <summary>Coeficiente de variação que satura a penalidade de volatilidade (estabilidade = 0).</summary>
    public double VolatilitySaturation { get; }

    /// <summary>Janela usada para considerar histórico de preço na volatilidade.</summary>
    public TimeSpan VolatilityWindow { get; }

    private static void ValidateUnit(double value, string paramName)
    {
        if (double.IsNaN(value) || value < 0 || value > 1)
        {
            throw new ArgumentOutOfRangeException(paramName, "weight must be between 0 and 1.");
        }
    }
}
