namespace TradingIntel.Application.PlayerMarket;

/// <summary>
/// Active market data source. Bound to the configuration section
/// <c>Market</c> (key <c>Source</c>); the same value drives which
/// <see cref="IPlayerMarketClient"/> implementation gets registered in
/// Infrastructure DI <em>and</em> which <c>Source</c> prefix the read paths
/// (e.g. opportunity recompute) filter on so the persisted writes and reads
/// stay in sync.
/// </summary>
public sealed class MarketSourceOptions
{
    public const string SectionName = "Market";

    /// <summary>
    /// Lower-case source token. Defaults to <c>futgg</c> because the FUT.GG
    /// JSON API is reachable without Cloudflare. Set to <c>futbin</c> when a
    /// WAF-authorized proxy is wired up.
    /// </summary>
    public string Source { get; set; } = "futgg";

    /// <summary>
    /// Convenience accessor that returns the prefix used when filtering
    /// snapshots in storage. Snapshots are persisted with <c>Source</c> in the
    /// shape <c>&lt;source&gt;</c> or <c>&lt;source&gt;:&lt;platform&gt;</c>
    /// (see <c>FutGgMarketClient</c> / <c>FutbinMarketClient</c>) and the
    /// recompute path filters by <c>StartsWith(prefix)</c>.
    /// </summary>
    public string SourcePrefix
    {
        get
        {
            var token = (Source ?? string.Empty).Trim().ToLowerInvariant();
            return token.Length == 0 ? "futgg:" : $"{token}:";
        }
    }
}
