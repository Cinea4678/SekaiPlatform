using System.Text.Json;
using SekaiPlatform.Database;
using SekaiPlatform.Shared.Web.Context;
using SekaiPlatform.Shared.Web.Responses;

/// <summary>
/// Builds Asset Service read endpoint responses from persisted language assets.
/// </summary>
internal static class AssetsEndpointResults
{
    /// <summary>
    /// Converts a story group entity into its API response shape.
    /// </summary>
    public static StoryGroupResponse ToResponse(StoryGroup group)
    {
        return new StoryGroupResponse(
            group.Id,
            group.StoryType,
            group.ExternalType,
            group.ExternalId,
            group.DisplayNo,
            group.Title,
            group.Subtitle,
            ParseMetadata(group.Metadata),
            group.CreatedAt,
            group.UpdatedAt);
    }

    /// <summary>
    /// Converts a story entity into its API response shape.
    /// </summary>
    public static StoryResponse ToResponse(Story story)
    {
        return new StoryResponse(
            story.Id,
            story.Group is null ? null : ToResponse(story.Group),
            story.StoryType,
            story.ScenarioId,
            story.Title,
            story.SortOrder,
            ParseMetadata(story.Metadata),
            story.CreatedAt,
            story.UpdatedAt);
    }

    /// <summary>
    /// Converts a source line entity into its API response shape.
    /// </summary>
    public static StorySourceLineResponse ToResponse(StorySourceLine line)
    {
        return new StorySourceLineResponse(
            line.Id,
            line.StoryId,
            line.LineNo,
            line.LineType,
            line.Speaker,
            line.Text,
            ParseMetadata(line.Metadata),
            line.CreatedAt,
            line.UpdatedAt);
    }

    /// <summary>
    /// Converts a translation version entity into its API response shape.
    /// </summary>
    public static TranslationVersionResponse ToResponse(TranslationVersion version)
    {
        return new TranslationVersionResponse(
            version.Id,
            version.StoryId,
            version.VersionNo,
            version.Title,
            ParseMetadata(version.Metadata),
            version.CreatedBy,
            version.CreatedAt,
            version.UpdatedAt);
    }

    /// <summary>
    /// Converts a translation line entity into its API response shape.
    /// </summary>
    public static TranslationLineResponse ToResponse(TranslationLine line)
    {
        return new TranslationLineResponse(
            line.Id,
            line.VersionId,
            line.SourceLineId,
            line.LineNo,
            line.Speaker,
            line.Text,
            ParseMetadata(line.Metadata),
            line.CreatedAt,
            line.UpdatedAt);
    }

    /// <summary>
    /// Creates a trace-aware error response for asset read endpoints.
    /// </summary>
    public static IResult Error(ICurrentRequestContextAccessor contextAccessor, int statusCode, string message)
    {
        var requestContext = contextAccessor.GetCurrent();
        return Results.Json(new ErrorResponse(message, requestContext.TraceId), statusCode: statusCode);
    }

    /// <summary>
    /// Parses stored metadata JSON into a detached JSON element.
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
