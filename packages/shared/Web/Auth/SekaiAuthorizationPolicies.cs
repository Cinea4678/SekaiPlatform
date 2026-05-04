namespace SekaiPlatform.Shared.Web;

/// <summary>
/// Defines authorization policy names shared by platform services.
/// </summary>
public static class SekaiAuthorizationPolicies
{
    /// <summary>
    /// Policy requiring an authenticated platform user.
    /// </summary>
    public const string LoggedIn = "sekai.logged_in";

    /// <summary>
    /// Policy requiring an authenticated user with a selected tenant.
    /// </summary>
    public const string TenantSelected = "sekai.tenant_selected";
}
