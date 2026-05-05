using SekaiPlatform.Shared.Web.Search;
using SekaiPlatform.SourceSync;

namespace SekaiPlatform.SyncWorker;

/// <summary>
/// Background worker that runs scheduled source story synchronization jobs.
/// </summary>
/// <param name="logger">Logger for worker lifecycle and sync job outcomes.</param>
/// <param name="scopeFactory">Scope factory used to resolve scoped synchronization services per run.</param>
/// <param name="searchIndexRefreshClient">Client used to refresh search index documents after successful sync runs.</param>
/// <param name="options">Scheduling options for Moe Sekai source synchronization.</param>
public class Worker(
    ILogger<Worker> logger,
    IServiceScopeFactory scopeFactory,
    SearchIndexRefreshClient searchIndexRefreshClient,
    MoeSekaiSourceSyncOptions options) : BackgroundService
{
    /// <summary>
    /// Runs the scheduling loop until host shutdown is requested.
    /// </summary>
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
                var result = await runner.RunWithResultAsync(SourceSyncConstants.TriggerScheduled, stoppingToken);
                await RefreshStoryIndexesAsync(result, stoppingToken);
                logger.LogInformation(
                    "Scheduled source sync finished. job_id:{JobId} status:{Status}",
                    result.Job.Id,
                    result.Job.Status);
            }
            catch (SourceSyncAlreadyRunningException)
            {
                logger.LogInformation("Scheduled source sync skipped because another source sync is already running.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled source sync failed unexpectedly and will be retried on next schedule.");
            }
        }
    }

    /// <summary>
    /// Calculates the delay from now until the next configured local run time.
    /// </summary>
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

    /// <summary>
    /// Requests search index refresh for stories updated by a successful scheduled sync.
    /// </summary>
    private async Task RefreshStoryIndexesAsync(
        SourceStorySyncRunResult result,
        CancellationToken cancellationToken)
    {
        if (result.Job.Status != SourceSyncConstants.StatusSucceeded || result.SyncedStoryIds.Count == 0)
        {
            return;
        }

        var refresh = await searchIndexRefreshClient.RefreshStoriesAsync(result.SyncedStoryIds, cancellationToken);
        if (!refresh.Success)
        {
            logger.LogError(
                "Search index refresh failed. status:{StatusCode} body:{Body}",
                refresh.StatusCode,
                refresh.Body);
        }
    }
}
