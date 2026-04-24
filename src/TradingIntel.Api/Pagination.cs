namespace TradingIntel.Api;

internal static class Pagination
{
    public const int DefaultPageSize = 50;

    public const int MaxPageSize = 200;

    public static (int Page, int PageSize, int Skip) Normalize(int? page, int? pageSize)
    {
        var p = page is null or < 1 ? 1 : page.Value;
        var s = pageSize is null ? DefaultPageSize : Math.Clamp(pageSize.Value, 1, MaxPageSize);
        return (p, s, (p - 1) * s);
    }

    public static int TotalPages(int totalItems, int pageSize) =>
        pageSize <= 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
}
