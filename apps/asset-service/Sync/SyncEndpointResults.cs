using System.Text.Json;
using SekaiPlatform.Database;
using SekaiPlatform.Shared.Web;

internal static class SyncEndpointResults
{
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

    public static IResult Error(ICurrentRequestContextAccessor contextAccessor, int statusCode, string message)
    {
        var requestContext = contextAccessor.GetCurrent();
        return Results.Json(new ErrorResponse(message, requestContext.TraceId), statusCode: statusCode);
    }

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
