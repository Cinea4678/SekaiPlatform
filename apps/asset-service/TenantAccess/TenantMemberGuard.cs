using Microsoft.EntityFrameworkCore;
using SekaiPlatform.Database;
using SekaiPlatform.Shared.Web.Context;

/// <summary>
/// Checks active tenant membership for Asset Service read endpoints.
/// </summary>
internal static class TenantMemberGuard
{
    /// <summary>
    /// Checks whether the delegated user still belongs to the selected tenant.
    /// </summary>
    public static async Task<bool> IsCurrentTenantMemberAsync(
        SekaiPlatformDbContext dbContext,
        ICurrentRequestContextAccessor contextAccessor,
        CancellationToken cancellationToken)
    {
        var context = contextAccessor.GetCurrent();
        if (context.UserId is null || context.TenantId is null)
        {
            return false;
        }

        return await dbContext.UserTenants.AnyAsync(item =>
            item.TenantId == context.TenantId.Value
            && item.UserId == context.UserId.Value
            && item.Status == UserTenantStatuses.Active,
            cancellationToken);
    }
}
