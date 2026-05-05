using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Response item describing a supported platform story type.
/// </summary>
internal sealed record StoryTypeInfoResponse(
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("label")] string Label);

/// <summary>
/// Shared pagination metadata used by Asset Service list endpoints.
/// </summary>
internal sealed record PageMetaResponse(
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("page_size")] int PageSize,
    [property: JsonPropertyName("total")] int Total);

/// <summary>
/// Response body for paged story group queries.
/// </summary>
internal sealed record StoryGroupPageResponse(
    [property: JsonPropertyName("items")] IReadOnlyList<StoryGroupResponse> Items,
    [property: JsonPropertyName("page")] PageMetaResponse Page);

/// <summary>
/// Response body for paged story queries.
/// </summary>
internal sealed record StoryPageResponse(
    [property: JsonPropertyName("items")] IReadOnlyList<StoryResponse> Items,
    [property: JsonPropertyName("page")] PageMetaResponse Page);

/// <summary>
/// Response body for paged tenant translation version queries.
/// </summary>
internal sealed record TranslationVersionPageResponse(
    [property: JsonPropertyName("items")] IReadOnlyList<TranslationVersionResponse> Items,
    [property: JsonPropertyName("page")] PageMetaResponse Page);

/// <summary>
/// Response describing a shared story group.
/// </summary>
internal sealed record StoryGroupResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("story_type")] string StoryType,
    [property: JsonPropertyName("external_type")] string? ExternalType,
    [property: JsonPropertyName("external_id")] string? ExternalId,
    [property: JsonPropertyName("display_no")] int? DisplayNo,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("subtitle")] string? Subtitle,
    [property: JsonPropertyName("metadata")] JsonElement? Metadata,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt);

/// <summary>
/// Response describing a shared story and its optional group.
/// </summary>
internal sealed record StoryResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("group")] StoryGroupResponse? Group,
    [property: JsonPropertyName("story_type")] string StoryType,
    [property: JsonPropertyName("scenario_id")] string ScenarioId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("sort_order")] int SortOrder,
    [property: JsonPropertyName("metadata")] JsonElement? Metadata,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt);

/// <summary>
/// Response describing one shared source line in a story.
/// </summary>
internal sealed record StorySourceLineResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("story_id")] long StoryId,
    [property: JsonPropertyName("line_no")] int LineNo,
    [property: JsonPropertyName("line_type")] string LineType,
    [property: JsonPropertyName("speaker")] string? Speaker,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("metadata")] JsonElement? Metadata,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt);

/// <summary>
/// Response describing a tenant-owned translation version.
/// </summary>
internal sealed record TranslationVersionResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("story_id")] long StoryId,
    [property: JsonPropertyName("version_no")] int VersionNo,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("metadata")] JsonElement? Metadata,
    [property: JsonPropertyName("created_by")] long CreatedBy,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt);

/// <summary>
/// Response describing one translated line in a tenant translation version.
/// </summary>
internal sealed record TranslationLineResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("version_id")] long VersionId,
    [property: JsonPropertyName("source_line_id")] long SourceLineId,
    [property: JsonPropertyName("line_no")] int LineNo,
    [property: JsonPropertyName("speaker")] string? Speaker,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("metadata")] JsonElement? Metadata,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt);
