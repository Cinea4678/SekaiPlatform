using System.Text.Json.Serialization;

/// <summary>
/// Frontend login payload forwarded to Auth Service.
/// </summary>
internal sealed record LoginRequest(
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password);

/// <summary>
/// Frontend tenant selection payload forwarded to Auth Service.
/// </summary>
internal sealed record SwitchTenantRequest(
    [property: JsonPropertyName("tenant_id")] long TenantId);

/// <summary>
/// Frontend invitation payload forwarded to Auth Service.
/// </summary>
internal sealed record InvitationRequest(
    [property: JsonPropertyName("qq_id")] string QqId,
    [property: JsonPropertyName("role")] string Role);

/// <summary>
/// Logout response returned after clearing the authentication cookie.
/// </summary>
internal sealed record LogoutResponse(
    [property: JsonPropertyName("ok")] bool Ok);

/// <summary>
/// Token subset parsed from Auth Service responses for cookie handling.
/// </summary>
internal sealed record AuthTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expires_at")] DateTimeOffset ExpiresAt);
