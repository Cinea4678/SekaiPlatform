using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SekaiPlatform.Database;

namespace SekaiPlatform.IntegrationTests;

/// <summary>
/// Provides a migrated PostgreSQL database with deterministic tenants and users for integration tests.
/// </summary>
public sealed class IntegrationTestDatabaseFixture : IAsyncLifetime
{
    /// <summary>
    /// Primary tenant name used by authenticated integration tests.
    /// </summary>
    public const string TenantName = "集成测试租户";

    /// <summary>
    /// Secondary tenant name used to verify tenant selection behavior.
    /// </summary>
    public const string SecondTenantName = "集成测试第二租户";

    /// <summary>
    /// QQ ID for the seeded super administrator.
    /// </summary>
    public const string AdminQqId = "900000000001";

    /// <summary>
    /// Display name for the seeded super administrator.
    /// </summary>
    public const string AdminDisplayName = "集成测试超级管理员";

    /// <summary>
    /// Password for the seeded super administrator.
    /// </summary>
    public const string AdminPassword = "sekai-integration-test-password";

    /// <summary>
    /// QQ ID for the seeded tenant administrator.
    /// </summary>
    public const string TenantAdminQqId = "900000000002";

    /// <summary>
    /// Password for the seeded tenant administrator.
    /// </summary>
    public const string TenantAdminPassword = "sekai-integration-test-admin-password";

    /// <summary>
    /// QQ ID for the seeded normal tenant member.
    /// </summary>
    public const string NormalUserQqId = "900000000003";

    /// <summary>
    /// Password for the seeded normal tenant member.
    /// </summary>
    public const string NormalUserPassword = "sekai-integration-test-normal-password";

    /// <summary>
    /// QQ ID for the seeded user that belongs to two tenants.
    /// </summary>
    public const string MultiTenantUserQqId = "900000000004";

    /// <summary>
    /// Password for the seeded user that belongs to two tenants.
    /// </summary>
    public const string MultiTenantUserPassword = "sekai-integration-test-multi-password";

    private const string ConnectionStringEnvironmentName = "SEKAI_INTEGRATION_TEST_POSTGRES";

    /// <summary>
    /// Database connection string used by service factories and direct DbContext checks.
    /// </summary>
    public string ConnectionString { get; } = CreateConnectionString();

    /// <summary>
    /// Applies migrations and seeds baseline tenants, users, and memberships.
    /// </summary>
    public async Task InitializeAsync()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
        await SeedAsync(dbContext);
    }

    /// <summary>
    /// Leaves the shared integration database available for subsequent tests in the collection.
    /// </summary>
    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates a DbContext connected to the shared integration database.
    /// </summary>
    public SekaiPlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SekaiPlatformDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new SekaiPlatformDbContext(options);
    }

    /// <summary>
    /// Ensures all baseline tenants, users, and memberships exist with current credentials.
    /// </summary>
    private static async Task SeedAsync(SekaiPlatformDbContext dbContext)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        var now = DateTimeOffset.UtcNow;

        var tenant = await EnsureTenantAsync(dbContext, TenantName);
        var secondTenant = await EnsureTenantAsync(dbContext, SecondTenantName);

        var admin = await EnsureUserAsync(dbContext, AdminQqId, AdminDisplayName, AdminPassword, now);
        var tenantAdmin = await EnsureUserAsync(dbContext, TenantAdminQqId, "集成测试管理员", TenantAdminPassword, now);
        var normalUser = await EnsureUserAsync(dbContext, NormalUserQqId, "集成测试普通用户", NormalUserPassword, now);
        var multiTenantUser = await EnsureUserAsync(dbContext, MultiTenantUserQqId, "集成测试多租户用户", MultiTenantUserPassword, now);

        await EnsureMembershipAsync(dbContext, tenant.Id, admin.Id, UserTenantRoles.SuperAdmin, now);
        await EnsureMembershipAsync(dbContext, tenant.Id, tenantAdmin.Id, UserTenantRoles.Admin, now);
        await EnsureMembershipAsync(dbContext, tenant.Id, normalUser.Id, UserTenantRoles.Normal, now);
        await EnsureMembershipAsync(dbContext, tenant.Id, multiTenantUser.Id, UserTenantRoles.Normal, now);
        await EnsureMembershipAsync(dbContext, secondTenant.Id, multiTenantUser.Id, UserTenantRoles.Normal, now);

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    /// <summary>
    /// Finds or creates a tenant by display name.
    /// </summary>
    private static async Task<Tenant> EnsureTenantAsync(SekaiPlatformDbContext dbContext, string name)
    {
        var tenant = await dbContext.Tenants.FirstOrDefaultAsync(item => item.Name == name);
        if (tenant is not null)
        {
            return tenant;
        }

        tenant = new Tenant { Name = name };
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();
        return tenant;
    }

    /// <summary>
    /// Finds or creates a user and refreshes the display name and password hash.
    /// </summary>
    private static async Task<User> EnsureUserAsync(
        SekaiPlatformDbContext dbContext,
        string qqId,
        string displayName,
        string password,
        DateTimeOffset now)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(item => item.QqId == qqId);
        if (user is null)
        {
            user = new User
            {
                QqId = qqId,
                CreatedAt = now
            };
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
        }

        user.DisplayName = displayName;
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
        user.UpdatedAt = now;
        return user;
    }

    /// <summary>
    /// Finds or creates an active tenant membership with the requested role.
    /// </summary>
    private static async Task EnsureMembershipAsync(
        SekaiPlatformDbContext dbContext,
        long tenantId,
        long userId,
        string role,
        DateTimeOffset now)
    {
        var membership = await dbContext.UserTenants.FindAsync(tenantId, userId);
        if (membership is null)
        {
            membership = new UserTenant
            {
                TenantId = tenantId,
                UserId = userId,
                CreatedAt = now
            };
            dbContext.UserTenants.Add(membership);
        }

        membership.Role = role;
        membership.Status = UserTenantStatuses.Active;
        membership.DeletedAt = null;
        membership.UpdatedAt = now;
    }

    /// <summary>
    /// Builds the PostgreSQL connection string from explicit or component environment variables.
    /// </summary>
    private static string CreateConnectionString()
    {
        var explicitConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentName);
        if (!string.IsNullOrWhiteSpace(explicitConnectionString))
        {
            return explicitConnectionString;
        }

        var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
        var database = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "sekai_platform";
        var username = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "sekai_platform";
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "sekai_platform";

        return $"Host={host};Port={port};Database={database};Username={username};Password={password}";
    }
}
