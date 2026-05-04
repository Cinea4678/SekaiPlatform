using System.Text.Json;
using SekaiPlatform.Database;
using SekaiPlatform.Shared.Web;

/// <summary>
/// Builds Asset Service synchronization endpoint responses.
/// </summary>
internal static class SyncEndpointResults
{
    /// <summary>
    /// Converts a persisted sync job into its API response shape.
    /// </summary>
    public static SyncJobResponse ToResponse(SyncJob job)
    {
        return new SyncJobResponse(
            job.Id,
            job.JobType,
            job.TriggerType,
            job.Status,
            job.StartedAt,
            job.EndedAt,
            job.ErrorMessage,
            ParseMetadata(job.Metadata),
            job.CreatedAt,
            job.UpdatedAt);
    }

    /// <summary>
    /// Creates a trace-aware error response for sync endpoints.
    /// </summary>
    public static IResult Error(ICurrentRequestContextAccessor contextAccessor, int statusCode, string message)
    {
        var requestContext = contextAccessor.GetCurrent();
        return Results.Json(new ErrorResponse(message, requestContext.TraceId), statusCode: statusCode);
    }

    /// <summary>
    /// Parses stored sync metadata JSON into a detached JSON element.
    /// </summary>
    private static JsonElement? ParseMetadata(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return null;
        }

        using var document = JsonDocument.Parse(metadata);
        return document.RootElement.Clone();
    }
}
