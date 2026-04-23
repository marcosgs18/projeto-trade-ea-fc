namespace TradingIntel.Application.Sbc;

/// <summary>
/// Motivo legível que contribuiu para o score de demanda de uma faixa de rating.
/// </summary>
public sealed record DemandReason
{
    public DemandReason(string code, string message, double weight)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("code cannot be null or whitespace.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("message cannot be null or whitespace.", nameof(message));
        }

        if (double.IsNaN(weight) || weight < 0 || weight > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(weight), "weight must be between 0 and 1.");
        }

        Code = code.Trim();
        Message = message.Trim();
        Weight = weight;
    }

    /// <summary>Código estável para agregação/telemetria (ex.: <c>REPEATABLE_UNLIMITED</c>).</summary>
    public string Code { get; }

    /// <summary>Mensagem pronta para exibição ao operador.</summary>
    public string Message { get; }

    /// <summary>Peso da contribuição normalizado em [0, 1].</summary>
    public double Weight { get; }
}
