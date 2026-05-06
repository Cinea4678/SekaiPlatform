using System.Text.Json.Serialization;

/// <summary>
/// Stable public error response returned by Open API endpoints.
/// </summary>
internal sealed record OpenApiErrorResponse(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("trace_id")] string TraceId);

/// <summary>
/// Request body for resolving multiple published translation scenarios.
/// </summary>
internal sealed record PublicTranslationBatchRequest(
    [property: JsonPropertyName("scenario_ids")] IReadOnlyList<string> ScenarioIds);
