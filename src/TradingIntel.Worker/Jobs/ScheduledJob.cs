using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradingIntel.Worker.Health;

namespace TradingIntel.Worker.Jobs;

/// <summary>
/// Base <see cref="BackgroundService"/> for a periodically scheduled collection job.
/// Handles scope creation per tick, per-tick try/catch, structured logging,
/// exponential backoff with a ceiling, and health reporting. Concrete jobs only
/// implement <see cref="ExecuteTickAsync"/>.
/// </summary>
public abstract class ScheduledJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IJobHealthRegistry _health;
    private readonly JobScheduleOptions _options;

    private TimeSpan _currentBackoff;
    private int _consecutiveFailures;

    protected ScheduledJob(
        string jobName,
        JobScheduleOptions options,
        IServiceScopeFactory scopeFactory,
        IJobHealthRegistry health,
        ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobName);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(health);
        ArgumentNullException.ThrowIfNull(logger);

        JobName = jobName;
        _options = options;
        _scopeFactory = scopeFactory;
        _health = health;
        Logger = logger;
        _currentBackoff = options.InitialBackoff;
    }

    public string JobName { get; }

    protected ILogger Logger { get; }

    /// <summary>Exposed for testing and diagnostics; not intended for business logic.</summary>
    public int ConsecutiveFailures => _consecutiveFailures;

    /// <summary>Exposed for testing and diagnostics; not intended for business logic.</summary>
    public TimeSpan CurrentBackoff => _currentBackoff;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            Logger.LogInformation("{Job} is disabled via configuration; not scheduling.", JobName);
            return;
        }

        Logger.LogInformation(
            "{Job} starting. interval={IntervalMs}ms initialDelay={InitialDelayMs}ms maxBackoff={MaxBackoffMs}ms",
            JobName,
            _options.Interval.TotalMilliseconds,
            _options.InitialDelay.TotalMilliseconds,
            _options.MaxBackoff.TotalMilliseconds);

        if (_options.InitialDelay > TimeSpan.Zero)
        {
            try
            {
                await Task.Delay(_options.InitialDelay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var tick = await RunTickAsync(stoppingToken).ConfigureAwait(false);
            if (tick.Status == TickStatus.Cancelled || stoppingToken.IsCancellationRequested)
            {
                return;
            }

            var delay = tick.Status == TickStatus.Success ? _options.Interval : _currentBackoff;
            var nextTickUtc = DateTime.UtcNow + delay;
            _health.RecordNextTick(JobName, nextTickUtc);
            Logger.LogInformation(
                "{Job} next tick in {DelayMs}ms at {NextTickUtc:O} (status={Status}).",
                JobName,
                delay.TotalMilliseconds,
                nextTickUtc,
                tick.Status);

            try
            {
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Runs a single tick end-to-end: creates a DI scope, invokes <see cref="ExecuteTickAsync"/>,
    /// catches and classifies exceptions, updates health + backoff, and logs metrics. Exposed as
    /// a public method so it can be driven by tests (and future admin endpoints) without spinning
    /// up the host.
    /// </summary>
    public async Task<TickResult> RunTickAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await ExecuteTickAsync(scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            OnSuccess(stopwatch.Elapsed);
            return new TickResult(TickStatus.Success, stopwatch.Elapsed, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            Logger.LogInformation(
                "{Job} tick cancelled after {ElapsedMs}ms.",
                JobName,
                stopwatch.Elapsed.TotalMilliseconds);
            return new TickResult(TickStatus.Cancelled, stopwatch.Elapsed, null);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            OnFailure(ex, stopwatch.Elapsed);
            return new TickResult(TickStatus.Failure, stopwatch.Elapsed, ex);
        }
    }

    protected abstract Task ExecuteTickAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken);

    private void OnSuccess(TimeSpan elapsed)
    {
        _consecutiveFailures = 0;
        _currentBackoff = _options.InitialBackoff;
        _health.RecordSuccess(JobName, DateTime.UtcNow, elapsed);
        Logger.LogInformation(
            "{Job} tick succeeded in {ElapsedMs}ms.",
            JobName,
            elapsed.TotalMilliseconds);
    }

    private void OnFailure(Exception ex, TimeSpan elapsed)
    {
        _consecutiveFailures++;

        Logger.LogError(
            ex,
            "{Job} tick failed in {ElapsedMs}ms. consecutiveFailures={ConsecutiveFailures} currentBackoffMs={BackoffMs}",
            JobName,
            elapsed.TotalMilliseconds,
            _consecutiveFailures,
            _currentBackoff.TotalMilliseconds);

        _health.RecordFailure(JobName, DateTime.UtcNow, elapsed, ex.Message, _consecutiveFailures);

        var nextBackoffMs = _currentBackoff.TotalMilliseconds * _options.BackoffMultiplier;
        var next = TimeSpan.FromMilliseconds(nextBackoffMs);
        _currentBackoff = next > _options.MaxBackoff ? _options.MaxBackoff : next;
    }
}
