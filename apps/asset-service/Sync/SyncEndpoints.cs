using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SekaiPlatform.Database;
using SekaiPlatform.Shared.Web;
using SekaiPlatform.SourceSync;

internal static class SyncEndpoints
{
    public static IEndpointRouteBuilder MapSyncEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/internal/sync/jobs", async Task<IResult> (
            HttpContext httpContext,
            SekaiPlatformDbContext dbContext,
            SourceStorySyncRunner syncRunner,
            ICurrentRequestContextAccessor contextAccessor,
            CancellationToken cancellationToken) =>
        {
            if (!await IsCurrentTenantAdminAsync(dbContext, contextAccessor, cancellationToken))
            {
                return SyncEndpointResults.Error(contextAccessor, StatusCodes.Status403Forbidden, "Forbidden.");
            }

            var requestResult = await ReadRequestAsync(httpContext, cancellationToken);
            if (!requestResult.Success)
            {
                return SyncEndpointResults.Error(contextAccessor, StatusCodes.Status400BadRequest, "Invalid sync job request.");
            }

            var request = requestResult.Request;
            if (!string.IsNullOrWhiteSpace(request?.Source)
                && !string.Equals(request.Source, SourceSyncConstants.Source, StringComparison.Ordinal))
            {
                return SyncEndpointResults.Error(contextAccessor, StatusCodes.Status400BadRequest, "Unsupported sync source.");
            }

            SyncJob job;
            try
            {
                job = await syncRunner.RunAsync(SourceSyncConstants.TriggerManual, CancellationToken.None);
            }
            catch (SourceSyncAlreadyRunningException)
            {
                return SyncEndpointResults.Error(contextAccessor, StatusCodes.Status409Conflict, "Source story sync is already running.");
            }

            return Results.Json(SyncEndpointResults.ToResponse(job));
        }).RequireAuthorization(SekaiAuthorizationPolicies.TenantSelected);

        app.MapGet("/internal/sync/jobs", async Task<IResult> (
            int? limit,
            SekaiPlatformDbContext dbContext,
            ICurrentRequestContextAccessor contextAccessor,
            CancellationToken cancellationToken) =>
        {
            if (!await IsCurrentTenantAdminAsync(dbContext, contextAccessor, cancellationToken))
            {
                return SyncEndpointResults.Error(contextAccessor, StatusCodes.Status403Forbidden, "Forbidden.");
            }

            var take = Math.Clamp(limit ?? 20, 1, 100);
            var jobs = await dbContext.SyncJobs
                .OrderByDescending(job => job.CreatedAt)
                .Take(take)
                .ToArrayAsync(cancellationToken);

            return Results.Json(jobs.Select(SyncEndpointResults.ToResponse).ToArray());
        }).RequireAuthorization(SekaiAuthorizationPolicies.TenantSelected);

        app.MapGet("/internal/sync/jobs/{syncJobId:long}", async Task<IResult> (
            long syncJobId,
            SekaiPlatformDbContext dbContext,
            ICurrentRequestContextAccessor contextAccessor,
            CancellationToken cancellationToken) =>
        {
            if (!await IsCurrentTenantAdminAsync(dbContext, contextAccessor, cancellationToken))
            {
                return SyncEndpointResults.Error(contextAccessor, StatusCodes.Status403Forbidden, "Forbidden.");
            }

            var job = await dbContext.SyncJobs.FindAsync([syncJobId], cancellationToken);
            return job is null
                ? SyncEndpointResults.Error(contextAccessor, StatusCodes.Status404NotFound, "Sync job not found.")
                : Results.Json(SyncEndpointResults.ToResponse(job));
        }).RequireAuthorization(SekaiAuthorizationPolicies.TenantSelected);

        return app;
    }

    private static async Task<bool> IsCurrentTenantAdminAsync(
        SekaiPlatformDbContext dbContext,
        ICurrentRequestContextAccessor contextAccessor,
        CancellationToken cancellationToken)
    {
        var context = contextAccessor.GetCurrent();
        if (context.UserId is null || context.TenantId is null)
        {
            return false;
        }

        var role = await dbContext.UserTenants
            .Where(item =>
                item.TenantId == context.TenantId.Value
                && item.UserId == context.UserId.Value
                && item.Status == UserTenantStatuses.Active)
            .Select(item => item.Role)
            .SingleOrDefaultAsync(cancellationToken);

        return role is UserTenantRoles.Admin or UserTenantRoles.SuperAdmin;
    }

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

    private sealed record RequestReadResult(bool Success, SyncJobRequest? Request);
}
