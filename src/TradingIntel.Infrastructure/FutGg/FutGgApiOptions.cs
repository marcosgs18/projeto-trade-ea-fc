namespace TradingIntel.Infrastructure.FutGg;

/// <summary>
/// Configuration for the FUT.GG market adapter. Values are bound from
/// <c>Market:FutGg</c> (see <c>docs/source-futgg-market.md</c>). All fields
/// have safe defaults so the adapter is usable with an empty config block.
/// </summary>
public sealed class FutGgApiOptions
{
    public const string SectionName = "Market:FutGg";

    /// <summary>Absolute base URL for the JSON API. Must not end with a slash.</summary>
    public string BaseUrl { get; set; } = "https://www.fut.gg";

    /// <summary>FIFA/EA FC season segment used in the URL (e.g. <c>26</c>).</summary>
    public string Season { get; set; } = "26";

    /// <summary>
    /// Platforms to collect per player on each tick. Valid values: <c>pc</c>,
    /// <c>console</c>. Each platform is emitted as a separate
    /// <c>PlayerPriceSnapshot</c> with <c>Source = "futgg:&lt;platform&gt;"</c>.
    /// Left intentionally empty here because <see cref="List{T}"/> binding via
    /// <c>IConfiguration</c> appends rather than replaces; DI sets the safe
    /// default of <c>["pc"]</c> when the section is missing.
    /// </summary>
    public List<string> Platforms { get; set; } = new();

    /// <summary>
    /// User-Agent used for outbound requests. Default imitates a real browser
    /// because plain scripted agents sometimes get captcha'd even on the
    /// public JSON endpoint.
    /// </summary>
    public string UserAgent { get; set; } =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/145.0.0.0 Safari/537.36";
}
