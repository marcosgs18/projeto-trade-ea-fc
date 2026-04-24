namespace TradingIntel.Api.Contracts;

/// <summary>
/// Leitura externa de uma entrada do watchlist (<c>tracked_players</c>).
/// </summary>
public sealed record TrackedPlayerResponse(
    long PlayerId,
    string DisplayName,
    int? Overall,
    string Source,
    DateTime AddedAtUtc,
    DateTime? LastCollectedAtUtc,
    bool IsActive);

/// <summary>
/// Criação/upsert de watchlist via <c>POST /api/watchlist</c>. Entradas
/// criadas por esta rota são gravadas com <c>Source = "Api"</c> e o seed de
/// boot nunca as sobrescreve.
/// </summary>
public sealed record CreateTrackedPlayerRequest(
    long PlayerId,
    string DisplayName,
    int? Overall);
