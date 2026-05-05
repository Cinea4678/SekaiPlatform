using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SekaiPlatform.Database;

/// <summary>
/// Applies configured database migration and development seed setup for API Service startup.
/// </summary>
internal static class DatabaseInitializer
{
    private const string DefaultTenantName = "PJS 字幕组";
    private const string AdminQqId = "1650121748";

    /// <summary>
    /// Runs configured database initialization steps.
    /// </summary>
    public static async Task InitializeAsync(WebApplication app)
    {
        var options = app.Configuration.GetSection("Database").Get<DatabaseOptions>() ?? new DatabaseOptions();
        if (!options.AutoMigrate && !options.Seed)
        {
            return;
        }

        if (options.Seed && !app.Environment.IsDevelopment())
        {
            throw new InvalidOperationException("Database seed is only allowed in Development.");
        }

        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SekaiPlatformDbContext>();

        if (options.AutoMigrate)
        {
            await dbContext.Database.MigrateAsync();
        }

        if (options.Seed)
        {
            await SeedAsync(dbContext, options.SeedUsers);
        }
    }

    /// <summary>
    /// Seeds the default tenant, administrator user, and super administrator membership.
    /// </summary>
    private static async Task SeedAsync(SekaiPlatformDbContext dbContext, SeedUserOptions seedUsers)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        var now = DateTimeOffset.UtcNow;
        var tenant = await dbContext.Tenants.FirstOrDefaultAsync(item => item.Name == DefaultTenantName);
        if (tenant is null)
        {
            tenant = new Tenant { Name = DefaultTenantName };
            dbContext.Tenants.Add(tenant);
            await dbContext.SaveChangesAsync();
        }

        var admin = await EnsureUserAsync(dbContext, AdminQqId, "本地超级管理员", seedUsers.AdminPassword, now);

        await EnsureMembershipAsync(dbContext, tenant.Id, admin.Id, UserTenantRoles.SuperAdmin, now);

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    /// <summary>
    /// Finds or creates a seed user and applies an initial password when configured.
    /// </summary>
    private static async Task<User> EnsureUserAsync(
        SekaiPlatformDbContext dbContext,
        string qqId,
        string displayName,
        string? password,
        DateTimeOffset now)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(item => item.QqId == qqId);
        if (user is null)
        {
            user = new User
            {
                QqId = qqId,
                DisplayName = displayName,
                CreatedAt = now,
                UpdatedAt = now
            };
            if (!string.IsNullOrWhiteSpace(password))
            {
                user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
            }

            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
        }

        return user;
    }

    /// <summary>
    /// Ensures the seed user has the required tenant membership.
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
            dbContext.UserTenants.Add(new UserTenant
            {
                TenantId = tenantId,
                UserId = userId,
                Role = role,
                Status = UserTenantStatuses.Active,
                CreatedAt = now,
                UpdatedAt = now
            });
        }
    }

    /// <summary>
    /// Development database initialization options.
    /// </summary>
    private sealed class DatabaseOptions
    {
        /// <summary>
        /// Gets or sets whether startup should apply pending EF Core migrations.
        /// </summary>
        public bool AutoMigrate { get; set; }

        /// <summary>
        /// Gets or sets whether startup should seed development users and memberships.
        /// </summary>
        public bool Seed { get; set; }

        /// <summary>
        /// Gets or sets credentials used when creating development seed users.
        /// </summary>
        public SeedUserOptions SeedUsers { get; set; } = new();
    }

    /// <summary>
    /// Seed user credentials used for local development.
    /// </summary>
    private sealed class SeedUserOptions
    {
        /// <summary>
        /// Gets or sets the password assigned to the default super administrator.
        /// </summary>
        public string? AdminPassword { get; set; }
    }
}
