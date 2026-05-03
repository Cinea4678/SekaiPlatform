namespace SekaiPlatform.Database;

public static class UserTenantRoles
{
    public const string Normal = "normal";

    public const string Admin = "admin";

    public const string SuperAdmin = "super_admin";

    public static readonly string[] All = [Normal, Admin, SuperAdmin];
}
