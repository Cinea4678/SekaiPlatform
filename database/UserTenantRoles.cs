namespace SekaiPlatform.Database;

/// <summary>
/// Defines the supported tenant membership roles.
/// </summary>
public static class UserTenantRoles
{
    /// <summary>
    /// Role for users who can search and view tenant assets.
    /// </summary>
    public const string Normal = "normal";

    /// <summary>
    /// Role for tenant administrators who can invite users and import translations.
    /// </summary>
    public const string Admin = "admin";

    /// <summary>
    /// Role for super administrators who can manage tenant administrators.
    /// </summary>
    public const string SuperAdmin = "super_admin";

    /// <summary>
    /// Gets all supported tenant role values.
    /// </summary>
    public static readonly string[] All = [Normal, Admin, SuperAdmin];
}
