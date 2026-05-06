using System.Text.Json.Serialization;

/// <summary>
/// Request body for resolving multiple published translation scenarios.
/// </summary>
internal sealed record PublicTranslationBatchRequest(
    [property: JsonPropertyName("scenario_ids")] IReadOnlyList<string> ScenarioIds);

/// <summary>
/// Response body for resolving multiple published translation scenarios.
/// </summary>
internal sealed record PublicTranslationBatchResponse(
    [property: JsonPropertyName("items")] IReadOnlyList<PublicTranslationResult> Items);

/// <summary>
/// Public translation lookup result for one scenario identifier.
/// </summary>
internal sealed record PublicTranslationResult(
    [property: JsonPropertyName("scenario_id")] string ScenarioId,
    [property: JsonPropertyName("has_translation")] bool HasTranslation,
    [property: JsonPropertyName("translations")] IReadOnlyList<PublicTranslationInfo> Translations);

/// <summary>
/// Published translation version information returned to Open API callers.
/// </summary>
internal sealed record PublicTranslationInfo(
    [property: JsonPropertyName("translation_version_id")] long TranslationVersionId,
    [property: JsonPropertyName("version_no")] int VersionNo,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("tenant")] PublicTenant Tenant,
    [property: JsonPropertyName("staff")] PublicTranslationStaff Staff,
    [property: JsonPropertyName("line_count")] int LineCount,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("lines")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<PublicTranslationLine>? Lines);

/// <summary>
/// Public tenant information attached to a published translation version.
/// </summary>
internal sealed record PublicTenant(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("avatar_url")] string? AvatarUrl);

/// <summary>
/// Staff credits attached to a published translation version.
/// </summary>
internal sealed record PublicTranslationStaff(
    [property: JsonPropertyName("translator")] string? Translator,
    [property: JsonPropertyName("proofreader")] string? Proofreader,
    [property: JsonPropertyName("approver")] string? Approver);

/// <summary>
/// Published translated line returned by the single-scenario Open API endpoint.
/// </summary>
internal sealed record PublicTranslationLine(
    [property: JsonPropertyName("line_no")] int LineNo,
    [property: JsonPropertyName("line_type")] string LineType,
    [property: JsonPropertyName("speaker")] string? Speaker,
    [property: JsonPropertyName("text")] string Text);
