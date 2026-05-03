using System.Text.Json.Serialization;

namespace SekaiPlatform.Shared.Web;

public sealed record ErrorResponse(
    [property: JsonPropertyName("msg")] string Msg,
    [property: JsonPropertyName("trace_id")] string TraceId);
