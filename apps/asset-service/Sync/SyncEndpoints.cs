using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SekaiPlatform.Database;
using SekaiPlatform.Shared.Web.Auth;
using SekaiPlatform.Shared.Web.Context;
using SekaiPlatform.Shared.Web.Search;
using SekaiPlatform.SourceSync;

/// <summary>
/// Maps Asset Service endpoints that manage source story synchronization jobs.
/// </summary>
internal static class SyncEndpoints
{
    /// <summary>
    /// Registers internal synchronization job endpoints for manual runs and job inspection.
    /// </summary>
    public static IEndpointRouteBuilder MapSyncEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/internal/sync/jobs", async Task<IResult> (
            HttpContext httpContext,
            SekaiPlatformDbContext dbContext,
            SourceStorySyncRunner syncRunner,
            IServiceScopeFactory serviceScopeFactory,
            ICurrentRequestContextAccessor contextAccessor,
            ILogger<SourceStorySyncRunner> logger,
            CancellationToken cancellationToken) =>
        {
            if (!await TenantAdminGuard.IsCurrentTenantAdminAsync(dbContext, contextAccessor, cancellationToken))
            {
                return SyncEndpointResults.Error(contextAccessor, StatusCodes.Status403Forbidden, "无权访问。");
            }

            var requestResult = await ReadRequestAsync(httpContext, cancellationToken);
            if (!requestResult.Success)
            {
                return SyncEndpointResults.Error(contextAccessor, StatusCodes.Status400BadRequest, "同步任务请求无效。");
            }

            var request = requestResult.Request;
            if (!string.IsNullOrWhiteSpace(request?.Source)
                && !string.Equals(request.Source, SourceSyncConstants.Source, StringComparison.Ordinal))
            {
                return SyncEndpointResults.Error(contextAccessor, StatusCodes.Status400BadRequest, "不支持的同步来源。");
            }

            SyncJob job;
            try
            {
                job = await syncRunner.CreatePendingJobAsync(SourceSyncConstants.TriggerManual, cancellationToken);
            }
            catch (SourceSyncAlreadyRunningException)
            {
                return SyncEndpointResults.Error(contextAccessor, StatusCodes.Status409Conflict, "原文同步任务正在运行。");
            }

            QueueManualSync(serviceScopeFactory, job.Id, logger);

            return Results.Json(SyncEndpointResults.ToResponse(job), statusCode: StatusCodes.Status202Accepted);
        }).RequireInternalAuthorization(
            SekaiInternalAuthDefaults.SyncJobsWriteScope,
            [SekaiInternalAuthDefaults.ApiServiceActor],
            requireSubject: true,
            requireTenant: true);

        app.MapGet("/internal/sync/jobs", async Task<IResult> (
            int? limit,
            SekaiPlatformDbContext dbContext,
            ICurrentRequestContextAccessor contextAccessor,
            CancellationToken cancellationToken) =>
        {
            if (!await TenantAdminGuard.IsCurrentTenantAdminAsync(dbContext, contextAccessor, cancellationToken))
            {
                return SyncEndpointResults.Error(contextAccessor, StatusCodes.Status403Forbidden, "无权访问。");
            }

            var take = Math.Clamp(limit ?? 20, 1, 100);
            var jobs = await dbContext.SyncJobs
                .OrderByDescending(job => job.CreatedAt)
                .Take(take)
                .ToArrayAsync(cancellationToken);

            return Results.Json(jobs.Select(SyncEndpointResults.ToResponse).ToArray());
        }).RequireInternalAuthorization(
            SekaiInternalAuthDefaults.SyncJobsReadScope,
            [SekaiInternalAuthDefaults.ApiServiceActor],
            requireSubject: true,
            requireTenant: true);

        app.MapGet("/internal/sync/jobs/{syncJobId:long}", async Task<IResult> (
            long syncJobId,
            SekaiPlatformDbContext dbContext,
            ICurrentRequestContextAccessor contextAccessor,
            CancellationToken cancellationToken) =>
        {
            if (!await TenantAdminGuard.IsCurrentTenantAdminAsync(dbContext, contextAccessor, cancellationToken))
            {
                return SyncEndpointResults.Error(contextAccessor, StatusCodes.Status403Forbidden, "无权访问。");
            }

            var job = await dbContext.SyncJobs.FindAsync([syncJobId], cancellationToken);
            return job is null
                ? SyncEndpointResults.Error(contextAccessor, StatusCodes.Status404NotFound, "同步任务不存在。")
                : Results.Json(SyncEndpointResults.ToResponse(job));
        }).RequireInternalAuthorization(
            SekaiInternalAuthDefaults.SyncJobsReadScope,
            [SekaiInternalAuthDefaults.ApiServiceActor],
            requireSubject: true,
            requireTenant: true);

        return app;
    }

    /// <summary>
    /// Starts the manual sync job in a background service scope after the trigger response is sent.
    /// </summary>
    private static void QueueManualSync(
        IServiceScopeFactory serviceScopeFactory,
        long syncJobId,
        ILogger logger)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = serviceScopeFactory.CreateAsyncScope();
                var syncRunner = scope.ServiceProvider.GetRequiredService<SourceStorySyncRunner>();
                var searchIndexRefreshClient = scope.ServiceProvider.GetRequiredService<SearchIndexRefreshClient>();

                var result = await syncRunner.RunPendingJobWithResultAsync(syncJobId, CancellationToken.None);
                await RefreshStoryIndexesAsync(result, searchIndexRefreshClient, logger, CancellationToken.None);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Manual source sync background task failed unexpectedly. job_id:{JobId}",
                    syncJobId);
            }
        });
    }

    /// <summary>
    /// Requests a search index refresh for stories changed by a successful synchronization run.
    /// </summary>
    private static async Task RefreshStoryIndexesAsync(
        SourceStorySyncRunResult result,
        SearchIndexRefreshClient searchIndexRefreshClient,
        ILogger logger,
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

    /// <summary>
    /// Reads an optional sync job request body and reports malformed JSON without throwing.
    /// </summary>
    private static async Task<RequestReadResult> ReadRequestAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (httpContext.Request.ContentLength is null or 0)
        {
            using var reader = new StreamReader(httpContext.Request.Body);
            var body = await reader.ReadToEndAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(body))
            {
                return new RequestReadResult(true, null);
            }

            return DeserializeRequest(body);
        }

        try
        {
            var request = await JsonSerializer.DeserializeAsync<SyncJobRequest>(
                httpContext.Request.Body,
                cancellationToken: cancellationToken);
            return new RequestReadResult(true, request);
        }
        catch (JsonException)
        {
            return new RequestReadResult(false, null);
        }
    }

    /// <summary>
    /// Deserializes the manual sync payload into a request result.
    /// </summary>
    private static RequestReadResult DeserializeRequest(string body)
    {
        try
        {
            return new RequestReadResult(true, JsonSerializer.Deserialize<SyncJobRequest>(body));
        }
        catch (JsonException)
        {
            return new RequestReadResult(false, null);
        }
    }

    /// <summary>
    /// Captures whether request body parsing succeeded and the parsed payload, if present.
    /// </summary>
    private sealed record RequestReadResult(bool Success, SyncJobRequest? Request);
}
