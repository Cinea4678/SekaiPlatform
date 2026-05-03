using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SekaiPlatform.Database;

internal static class DatabaseInitializer
{
    private const string DefaultTenantName = "PJS 字幕组";
    private const string AdminQqId = "1650121748";

    public static async Task InitializeAsync(WebApplication app)
    {
        var options = app.Configuration.GetSection("Database").Get<DatabaseOptions>() ?? new DatabaseOptions();
        if (!options.AutoMigrate && !options.Seed)
        {
            return;
        }

        if (!app.Environment.IsDevelopment())
        {
            throw new InvalidOperationException("Database auto migration and seed are only allowed in Development.");
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

    private sealed class DatabaseOptions
    {
        public bool AutoMigrate { get; set; }
        public bool Seed { get; set; }
        public SeedUserOptions SeedUsers { get; set; } = new();
    }

    private sealed class SeedUserOptions
    {
        public string? AdminPassword { get; set; }
    }
}
