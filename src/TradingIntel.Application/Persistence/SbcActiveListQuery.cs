namespace TradingIntel.Application.Persistence;

/// <summary>
/// Filtros para listagem paginada de SBCs exposta pela API (<c>/api/sbcs/active</c>).
/// </summary>
public sealed record SbcActiveListQuery(
    DateTime ActiveAsOfUtc,
    string? CategoryContains,
    DateTime? ExpiresBeforeUtc,
    int? RequiresOverall,
    bool IncludeExpired);
