namespace SekaiPlatform.Shared.Web;

/// <summary>
/// Defines shared authentication cookie and claim names.
/// </summary>
public static class SekaiAuthDefaults
{
    /// <summary>
    /// Name of the cookie that stores the platform bearer token.
    /// </summary>
    public const string AuthenticationCookieName = "SEKAI_PLATFORM_AUTH";

    /// <summary>
    /// Claim type used for the authenticated platform user identifier.
    /// </summary>
    public const string UserIdClaimType = "user_id";

    /// <summary>
    /// Claim type used for the selected tenant identifier.
    /// </summary>
    public const string TenantIdClaimType = "tenant_id";
}
