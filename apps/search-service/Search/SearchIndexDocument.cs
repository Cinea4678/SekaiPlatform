using System.Text.Json.Serialization;

namespace SekaiPlatform.SearchService.Search;

/// <summary>
/// Unified Elasticsearch document for shared source lines and tenant translation lines.
/// </summary>
internal sealed record SearchIndexDocument
{
    /// <summary>
    /// Gets the stable Elasticsearch document identifier.
    /// </summary>
    [JsonIgnore]
    public string DocumentId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the indexed asset type.
    /// </summary>
    [JsonPropertyName("asset_type")]
    public required string AssetType { get; init; }

    /// <summary>
    /// Gets the tenant identifier for translation documents, or null for shared source documents.
    /// </summary>
    [JsonPropertyName("tenant_id")]
    public long? TenantId { get; init; }

    /// <summary>
    /// Gets the story identifier containing this line.
    /// </summary>
    [JsonPropertyName("story_id")]
    public required long StoryId { get; init; }

    /// <summary>
    /// Gets the normalized story type.
    /// </summary>
    [JsonPropertyName("story_type")]
    public required string StoryType { get; init; }

    /// <summary>
    /// Gets the upstream scenario identifier.
    /// </summary>
    [JsonPropertyName("scenario_id")]
    public required string ScenarioId { get; init; }

    /// <summary>
    /// Gets the story title.
    /// </summary>
    [JsonPropertyName("story_title")]
    public required string StoryTitle { get; init; }

    /// <summary>
    /// Gets the optional story group identifier.
    /// </summary>
    [JsonPropertyName("story_group_id")]
    public long? StoryGroupId { get; init; }

    /// <summary>
    /// Gets the optional story group title.
    /// </summary>
    [JsonPropertyName("story_group_title")]
    public string? StoryGroupTitle { get; init; }

    /// <summary>
    /// Gets the translation version identifier for translation documents.
    /// </summary>
    [JsonPropertyName("translation_version_id")]
    public long? TranslationVersionId { get; init; }

    /// <summary>
    /// Gets the source line identifier used for story detail navigation.
    /// </summary>
    [JsonPropertyName("source_line_id")]
    public required long SourceLineId { get; init; }

    /// <summary>
    /// Gets the line number within the story or translation version.
    /// </summary>
    [JsonPropertyName("line_no")]
    public required int LineNo { get; init; }

    /// <summary>
    /// Gets the optional speaker name.
    /// </summary>
    [JsonPropertyName("speaker")]
    public string? Speaker { get; init; }

    /// <summary>
    /// Gets the indexed line text.
    /// </summary>
    [JsonPropertyName("text")]
    public required string Text { get; init; }
}
