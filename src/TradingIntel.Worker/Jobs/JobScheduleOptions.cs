namespace TradingIntel.Worker.Jobs;

/// <summary>
/// Per-job scheduling knobs bound from configuration
/// (<c>Jobs:&lt;JobName&gt;</c>). Intervals and backoffs use <see cref="TimeSpan"/>
/// strings in configuration, e.g. <c>"00:15:00"</c>.
/// </summary>
public class JobScheduleOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>Delay applied once before the first tick, to stagger jobs at startup.</summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Nominal interval between successful ticks.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>First delay used when a tick fails; resets on the next successful tick.</summary>
    public TimeSpan InitialBackoff { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Upper bound for exponential backoff.</summary>
    public TimeSpan MaxBackoff { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Multiplier applied to the backoff after each consecutive failure.</summary>
    public double BackoffMultiplier { get; set; } = 2.0;
}
