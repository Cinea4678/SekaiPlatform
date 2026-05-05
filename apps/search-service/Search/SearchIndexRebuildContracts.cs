using System.Text.Json.Serialization;

namespace SekaiPlatform.SearchService.Search;

/// <summary>
/// Request body for rebuilding Elasticsearch language asset documents from PostgreSQL.
/// </summary>
internal sealed class SearchIndexRebuildRequest
{
    /// <summary>
    /// Gets or sets the rebuild scope: all, source, or translation.
    /// </summary>
    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    /// <summary>
    /// Gets or sets whether the physical Elasticsearch index should be deleted and recreated.
    /// </summary>
    [JsonPropertyName("force_recreate")]
    public bool ForceRecreate { get; set; }

    /// <summary>
    /// Gets or sets optional story IDs used for source or translation partial rebuilds.
    /// </summary>
    [JsonPropertyName("story_ids")]
    public long[]? StoryIds { get; set; }

    /// <summary>
    /// Gets or sets an optional tenant ID used for translation partial rebuilds.
    /// </summary>
    [JsonPropertyName("tenant_id")]
    public long? TenantId { get; set; }

    /// <summary>
    /// Gets or sets an optional translation version ID used for translation partial rebuilds.
    /// </summary>
    [JsonPropertyName("translation_version_id")]
    public long? TranslationVersionId { get; set; }

    /// <summary>
    /// Gets or sets optional translation version IDs used for translation partial rebuilds.
    /// </summary>
    [JsonPropertyName("translation_version_ids")]
    public long[]? TranslationVersionIds { get; set; }
}

/// <summary>
/// Response body logged after a search index rebuild request completes.
/// </summary>
/// <param name="Scope">The normalized rebuild scope.</param>
/// <param name="Deleted">Whether existing matching index documents were deleted before indexing.</param>
/// <param name="SourceIndexed">Number of source documents indexed.</param>
/// <param name="TranslationIndexed">Number of translation documents indexed.</param>
internal sealed record SearchIndexRebuildResponse(
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("deleted")] bool Deleted,
    [property: JsonPropertyName("source_indexed")] int SourceIndexed,
    [property: JsonPropertyName("translation_indexed")] int TranslationIndexed);

/// <summary>
/// Response body returned after a search index rebuild request is accepted for background execution.
/// </summary>
/// <param name="JobId">Identifier used to correlate background rebuild logs.</param>
/// <param name="Scope">The normalized rebuild scope.</param>
/// <param name="Status">Accepted rebuild status.</param>
internal sealed record SearchIndexRebuildAcceptedResponse(
    [property: JsonPropertyName("job_id")] Guid JobId,
    [property: JsonPropertyName("scope")] string Scope,
    [property: JsonPropertyName("status")] string Status);
