using Microsoft.EntityFrameworkCore;
using SekaiPlatform.Database;
using SekaiPlatform.Shared.Web.Context;

/// <summary>
/// Checks tenant administration permissions for Asset Service business endpoints.
/// </summary>
internal static class TenantAdminGuard
{
    /// <summary>
    /// Checks whether the current request user can administer the selected tenant.
    /// </summary>
    public static async Task<bool> IsCurrentTenantAdminAsync(
        SekaiPlatformDbContext dbContext,
        ICurrentRequestContextAccessor contextAccessor,
        CancellationToken cancellationToken)
    {
        var context = contextAccessor.GetCurrent();
        if (context.UserId is null || context.TenantId is null)
        {
            return false;
        }

        var role = await dbContext.UserTenants
            .Where(item =>
                item.TenantId == context.TenantId.Value
                && item.UserId == context.UserId.Value
                && item.Status == UserTenantStatuses.Active)
            .Select(item => item.Role)
            .SingleOrDefaultAsync(cancellationToken);

        return role is UserTenantRoles.Admin or UserTenantRoles.SuperAdmin;
    }
}
