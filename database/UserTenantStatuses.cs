namespace SekaiPlatform.Database;

/// <summary>
/// Defines lifecycle statuses for tenant memberships.
/// </summary>
public static class UserTenantStatuses
{
    /// <summary>
    /// Status for memberships that can access tenant assets.
    /// </summary>
    public const string Active = "active";

    /// <summary>
    /// Status for memberships that are temporarily blocked.
    /// </summary>
    public const string Disabled = "disabled";

    /// <summary>
    /// Status for memberships that were soft-deleted.
    /// </summary>
    public const string Deleted = "deleted";
}
