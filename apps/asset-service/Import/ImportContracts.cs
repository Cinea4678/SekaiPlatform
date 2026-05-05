using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Request body for importing tenant translation versions in one batch.
/// </summary>
internal sealed class TranslationImportRequest
{
    /// <summary>
    /// Gets or sets the story translation versions to create.
    /// </summary>
    [JsonPropertyName("items")]
    public TranslationImportItem[]? Items { get; init; }
}

/// <summary>
/// Describes one story translation version to import.
/// </summary>
internal sealed class TranslationImportItem
{
    /// <summary>
    /// Gets or sets the platform story type used with scenario ID to match a story.
    /// </summary>
    [JsonPropertyName("story_type")]
    public string? StoryType { get; init; }

    /// <summary>
    /// Gets or sets the upstream scenario identifier used with story type to match a story.
    /// </summary>
    [JsonPropertyName("scenario_id")]
    public string? ScenarioId { get; init; }

    /// <summary>
    /// Gets or sets the optional imported translation version title.
    /// </summary>
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    /// <summary>
    /// Gets or sets translated lines keyed by source line number.
    /// </summary>
    [JsonPropertyName("lines")]
    public TranslationImportLine[]? Lines { get; init; }
}

/// <summary>
/// Describes one translated line in an imported version.
/// </summary>
internal sealed class TranslationImportLine
{
    /// <summary>
    /// Gets or sets the source story line number to translate.
    /// </summary>
    [JsonPropertyName("line_no")]
    public int LineNo { get; init; }

    /// <summary>
    /// Gets or sets the translated line text.
    /// </summary>
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    /// <summary>
    /// Gets or sets the optional translated speaker name.
    /// </summary>
    [JsonPropertyName("speaker")]
    public string? Speaker { get; init; }

    /// <summary>
    /// Gets or sets optional import metadata stored with the translated line.
    /// </summary>
    [JsonPropertyName("metadata")]
    public JsonElement? Metadata { get; init; }
}

/// <summary>
/// Response body returned after importing translation versions.
/// </summary>
internal sealed record TranslationImportResponse(
    [property: JsonPropertyName("items")] TranslationImportVersionResponse[] Items,
    [property: JsonPropertyName("total_versions")] int TotalVersions,
    [property: JsonPropertyName("total_lines")] int TotalLines);

/// <summary>
/// Response item describing one created translation version.
/// </summary>
internal sealed record TranslationImportVersionResponse(
    [property: JsonPropertyName("story_type")] string StoryType,
    [property: JsonPropertyName("scenario_id")] string ScenarioId,
    [property: JsonPropertyName("story_id")] long StoryId,
    [property: JsonPropertyName("translation_version_id")] long TranslationVersionId,
    [property: JsonPropertyName("version_no")] int VersionNo,
    [property: JsonPropertyName("line_count")] int LineCount);
