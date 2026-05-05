using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SekaiPlatform.Database;

namespace SekaiPlatform.SourceSync;

/// <summary>
/// Marks unfinished source sync jobs from previous service processes as failed on startup.
/// </summary>
public sealed class SourceStorySyncStartupRecoveryService(
    IServiceScopeFactory scopeFactory,
    ILogger<SourceStorySyncStartupRecoveryService> logger) : IHostedService
{
    /// <summary>
    /// Recovers stale pending or running source sync jobs when no live runner holds the runtime lock.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel startup recovery.</param>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SekaiPlatformDbContext>();
        var lockAcquired = await TryAcquireSyncLockAsync(dbContext, cancellationToken);
        if (!lockAcquired)
        {
            logger.LogInformation("Source sync startup recovery skipped because a live sync runner holds the lock.");
            return;
        }

        try
        {
            var staleJobs = await dbContext.SyncJobs
                .Where(job => job.JobType == SourceSyncConstants.JobType
                    && (job.Status == SourceSyncConstants.StatusPending
                        || job.Status == SourceSyncConstants.StatusRunning))
                .ToArrayAsync(cancellationToken);

            if (staleJobs.Length == 0)
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;
            foreach (var job in staleJobs)
            {
                job.Status = SourceSyncConstants.StatusFailed;
                job.EndedAt = now;
                job.UpdatedAt = now;
                job.ErrorMessage = "服务启动时检测到未完成的原文同步任务，已恢复为失败。";
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Recovered {Count} stale source sync jobs on startup.", staleJobs.Length);
        }
        finally
        {
            await ReleaseSyncLockAsync(dbContext);
        }
    }

    /// <summary>
    /// Completes immediately because recovery only runs during startup.
    /// </summary>
    /// <param name="cancellationToken">Unused stop token.</param>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private static async Task<bool> TryAcquireSyncLockAsync(
        SekaiPlatformDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var acquired = await dbContext.Database
                .SqlQueryRaw<bool>($"SELECT pg_try_advisory_lock({SourceStorySyncRunner.AdvisoryLockKey}) AS \"Value\"")
                .SingleAsync(cancellationToken);

            if (!acquired)
            {
                await dbContext.Database.CloseConnectionAsync();
            }

            return acquired;
        }
        catch
        {
            await dbContext.Database.CloseConnectionAsync();
            throw;
        }
    }

    private static async Task ReleaseSyncLockAsync(SekaiPlatformDbContext dbContext)
    {
        await dbContext.Database
            .SqlQueryRaw<bool>($"SELECT pg_advisory_unlock({SourceStorySyncRunner.AdvisoryLockKey}) AS \"Value\"")
            .SingleAsync(CancellationToken.None);
        await dbContext.Database.CloseConnectionAsync();
    }
}
