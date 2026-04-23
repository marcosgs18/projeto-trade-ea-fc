namespace TradingIntel.Worker;

public sealed class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("{Worker} started.", nameof(Worker));
        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
    }
}
