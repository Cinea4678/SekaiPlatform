using SekaiPlatform.SourceSync;

namespace SekaiPlatform.SyncWorker;

public class Worker(
    ILogger<Worker> logger,
    IServiceScopeFactory scopeFactory,
    MoeSekaiSourceSyncOptions options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Sync Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = GetDelayUntilNextRun(options.ScheduledLocalTime);
            logger.LogInformation("Next source sync scheduled in {Delay}.", delay);
            await Task.Delay(delay, stoppingToken);

            using var scope = scopeFactory.CreateScope();
            var runner = scope.ServiceProvider.GetRequiredService<SourceStorySyncRunner>();
            try
            {
                var job = await runner.RunAsync(SourceSyncConstants.TriggerScheduled, stoppingToken);
                logger.LogInformation(
                    "Scheduled source sync finished. job_id:{JobId} status:{Status}",
                    job.Id,
                    job.Status);
            }
            catch (SourceSyncAlreadyRunningException)
            {
                logger.LogInformation("Scheduled source sync skipped because another source sync is already running.");
            }
        }
    }

    private static TimeSpan GetDelayUntilNextRun(string scheduledLocalTime)
    {
        if (!TimeOnly.TryParse(scheduledLocalTime, out var time))
        {
            time = new TimeOnly(4, 0);
        }

        var now = DateTimeOffset.Now;
        var next = new DateTimeOffset(
            now.Year,
            now.Month,
            now.Day,
            time.Hour,
            time.Minute,
            0,
            now.Offset);
        if (next <= now)
        {
            next = next.AddDays(1);
        }

        return next - now;
    }
}
