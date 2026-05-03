using System.Text.Json.Serialization;

internal sealed record IssuedToken(
    string AccessToken,
    DateTimeOffset ExpiresAt);

internal sealed record LoginRequest(
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password);

internal sealed record SwitchTenantRequest(
    [property: JsonPropertyName("tenant_id")] long TenantId);

internal sealed record InvitationRequest(
    [property: JsonPropertyName("qq_id")] string? QqId,
    [property: JsonPropertyName("role")] string? Role);

internal sealed record AuthResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expires_at")] DateTimeOffset ExpiresAt,
    [property: JsonPropertyName("user")] UserProfileResponse User,
    [property: JsonPropertyName("current_tenant")] TenantMembershipResponse? CurrentTenant,
    [property: JsonPropertyName("tenants")] IReadOnlyCollection<TenantMembershipResponse> Tenants);

internal sealed record SessionResponse(
    [property: JsonPropertyName("user")] UserProfileResponse User,
    [property: JsonPropertyName("current_tenant")] TenantMembershipResponse? CurrentTenant,
    [property: JsonPropertyName("tenants")] IReadOnlyCollection<TenantMembershipResponse> Tenants);

internal sealed record UserProfileResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("qq_id")] string? QqId,
    [property: JsonPropertyName("display_name")] string? DisplayName,
    [property: JsonPropertyName("avatar_url")] string? AvatarUrl);

internal sealed record TenantMembershipResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("avatar_url")] string? AvatarUrl,
    [property: JsonPropertyName("role")] string Role);

internal sealed record InvitationMembershipResponse(
    [property: JsonPropertyName("tenant_id")] long TenantId,
    [property: JsonPropertyName("user_id")] long UserId,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("status")] string Status);

internal sealed record InvitationResponse(
    [property: JsonPropertyName("user")] UserProfileResponse User,
    [property: JsonPropertyName("membership")] InvitationMembershipResponse Membership,
    [property: JsonPropertyName("created_user")] bool CreatedUser,
    [property: JsonPropertyName("created_membership")] bool CreatedMembership,
    [property: JsonPropertyName("default_password")] string? DefaultPassword);
