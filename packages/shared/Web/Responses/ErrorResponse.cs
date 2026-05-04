using System.Text.Json.Serialization;

namespace SekaiPlatform.Shared.Web;

/// <summary>
/// Represents the standard JSON error payload returned by platform services.
/// </summary>
/// <param name="Msg">The user-facing error message.</param>
/// <param name="TraceId">The trace identifier for correlating the response with logs.</param>
public sealed record ErrorResponse(
    [property: JsonPropertyName("msg")] string Msg,
    [property: JsonPropertyName("trace_id")] string TraceId);
