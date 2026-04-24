using TradingIntel.Domain.ValueObjects;

namespace TradingIntel.Application.Persistence;

/// <summary>
/// Filtros opcionais para listagem paginada da watchlist via API admin.
/// Todos null = "ativos por padrão".
/// </summary>
public sealed record WatchlistQuery(
    bool IncludeInactive,
    WatchlistSource? Source,
    int? MinOverall);
