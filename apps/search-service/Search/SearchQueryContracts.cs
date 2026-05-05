using System.Text.Json.Serialization;

namespace SekaiPlatform.SearchService.Search;

/// <summary>
/// Request parameters for tenant-scoped language asset search.
/// </summary>
/// <param name="TenantId">The current tenant whose translations may be searched.</param>
/// <param name="Keyword">The non-empty keyword submitted by the user.</param>
/// <param name="Page">The one-based result page number.</param>
/// <param name="PageSize">The bounded number of hits returned per page.</param>
internal sealed record SearchQueryRequest(
    long TenantId,
    string Keyword,
    int Page,
    int PageSize);

/// <summary>
/// Paged response returned by the language asset search API.
/// </summary>
/// <param name="Items">Line-level search hits for the requested page.</param>
/// <param name="Total">Total number of matching Elasticsearch documents.</param>
/// <param name="Page">The one-based result page number.</param>
/// <param name="PageSize">The bounded number of hits returned per page.</param>
internal sealed record SearchQueryResponse(
    [property: JsonPropertyName("items")] IReadOnlyList<SearchQueryHit> Items,
    [property: JsonPropertyName("total")] long Total,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("page_size")] int PageSize);

/// <summary>
/// Line-level hit returned for shared source lines and tenant translation lines.
/// </summary>
internal sealed record SearchQueryHit
{
    /// <summary>
    /// Gets whether the hit came from shared source text or a tenant translation.
    /// </summary>
    [JsonPropertyName("asset_type")]
    public required string AssetType { get; init; }

    /// <summary>
    /// Gets the full indexed line text.
    /// </summary>
    [JsonPropertyName("text")]
    public required string Text { get; init; }

    /// <summary>
    /// Gets the highlighted hit text, or the full text when Elasticsearch returns no highlight.
    /// </summary>
    [JsonPropertyName("highlight_text")]
    public required string HighlightText { get; init; }

    /// <summary>
    /// Gets the optional speaker name for this line.
    /// </summary>
    [JsonPropertyName("speaker")]
    public string? Speaker { get; init; }

    /// <summary>
    /// Gets the line number inside the story or translation version.
    /// </summary>
    [JsonPropertyName("line_no")]
    public required int LineNo { get; init; }

    /// <summary>
    /// Gets the story identifier containing this line.
    /// </summary>
    [JsonPropertyName("story_id")]
    public required long StoryId { get; init; }

    /// <summary>
    /// Gets the story title containing this line.
    /// </summary>
    [JsonPropertyName("story_title")]
    public required string StoryTitle { get; init; }

    /// <summary>
    /// Gets the normalized story type.
    /// </summary>
    [JsonPropertyName("story_type")]
    public required string StoryType { get; init; }

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
    /// Gets the source line identifier used by story-detail navigation.
    /// </summary>
    [JsonPropertyName("source_line_id")]
    public required long SourceLineId { get; init; }

    /// <summary>
    /// Gets the translation version identifier when the hit comes from translated text.
    /// </summary>
    [JsonPropertyName("translation_version_id")]
    public long? TranslationVersionId { get; init; }
}
