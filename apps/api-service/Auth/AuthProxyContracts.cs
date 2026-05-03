using System.Text.Json.Serialization;

internal sealed record LoginRequest(
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password);

internal sealed record SwitchTenantRequest(
    [property: JsonPropertyName("tenant_id")] long TenantId);

internal sealed record InvitationRequest(
    [property: JsonPropertyName("qq_id")] string QqId,
    [property: JsonPropertyName("role")] string Role);

internal sealed record LogoutResponse(
    [property: JsonPropertyName("ok")] bool Ok);

internal sealed record AuthTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expires_at")] DateTimeOffset ExpiresAt);
