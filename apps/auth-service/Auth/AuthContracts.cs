using System.Text.Json.Serialization;

/// <summary>
/// Signed access token plus its absolute expiration time.
/// </summary>
internal sealed record IssuedToken(
    string AccessToken,
    DateTimeOffset ExpiresAt);

/// <summary>
/// Login credentials accepted by the Auth Service.
/// </summary>
internal sealed record LoginRequest(
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("password")] string Password);

/// <summary>
/// Tenant selection request for an authenticated user.
/// </summary>
internal sealed record SwitchTenantRequest(
    [property: JsonPropertyName("tenant_id")] long TenantId);

/// <summary>
/// Invitation request for adding a QQ user to the selected tenant.
/// </summary>
internal sealed record InvitationRequest(
    [property: JsonPropertyName("qq_id")] string? QqId,
    [property: JsonPropertyName("role")] string? Role);

/// <summary>
/// Authentication response containing token, user profile, and tenant context.
/// </summary>
internal sealed record AuthResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("expires_at")] DateTimeOffset ExpiresAt,
    [property: JsonPropertyName("user")] UserProfileResponse User,
    [property: JsonPropertyName("current_tenant")] TenantMembershipResponse? CurrentTenant,
    [property: JsonPropertyName("tenants")] IReadOnlyCollection<TenantMembershipResponse> Tenants);

/// <summary>
/// Current session response without issuing a new access token.
/// </summary>
internal sealed record SessionResponse(
    [property: JsonPropertyName("user")] UserProfileResponse User,
    [property: JsonPropertyName("current_tenant")] TenantMembershipResponse? CurrentTenant,
    [property: JsonPropertyName("tenants")] IReadOnlyCollection<TenantMembershipResponse> Tenants);

/// <summary>
/// User profile fields exposed to frontend callers.
/// </summary>
internal sealed record UserProfileResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("qq_id")] string? QqId,
    [property: JsonPropertyName("display_name")] string? DisplayName,
    [property: JsonPropertyName("avatar_url")] string? AvatarUrl);

/// <summary>
/// Tenant membership summary available to the current user.
/// </summary>
internal sealed record TenantMembershipResponse(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("avatar_url")] string? AvatarUrl,
    [property: JsonPropertyName("role")] string Role);

/// <summary>
/// Membership state returned after an invitation is created or reactivated.
/// </summary>
internal sealed record InvitationMembershipResponse(
    [property: JsonPropertyName("tenant_id")] long TenantId,
    [property: JsonPropertyName("user_id")] long UserId,
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("status")] string Status);

/// <summary>
/// Invitation result including created flags and any generated default password.
/// </summary>
internal sealed record InvitationResponse(
    [property: JsonPropertyName("user")] UserProfileResponse User,
    [property: JsonPropertyName("membership")] InvitationMembershipResponse Membership,
    [property: JsonPropertyName("created_user")] bool CreatedUser,
    [property: JsonPropertyName("created_membership")] bool CreatedMembership,
    [property: JsonPropertyName("default_password")] string? DefaultPassword);
