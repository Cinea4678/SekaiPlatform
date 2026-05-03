using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SekaiPlatform.Database;

namespace SekaiPlatform.IntegrationTests;

public sealed class IntegrationTestDatabaseFixture : IAsyncLifetime
{
    public const string TenantName = "集成测试租户";
    public const string AdminQqId = "900000000001";
    public const string AdminDisplayName = "集成测试超级管理员";
    public const string AdminPassword = "sekai-integration-test-password";

    private const string ConnectionStringEnvironmentName = "SEKAI_INTEGRATION_TEST_POSTGRES";

    public string ConnectionString { get; } = CreateConnectionString();

    public async Task InitializeAsync()
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
        await SeedAsync(dbContext);
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    public SekaiPlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SekaiPlatformDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new SekaiPlatformDbContext(options);
    }

    private static async Task SeedAsync(SekaiPlatformDbContext dbContext)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        var now = DateTimeOffset.UtcNow;

        var tenant = await dbContext.Tenants.FirstOrDefaultAsync(item => item.Name == TenantName);
        if (tenant is null)
        {
            tenant = new Tenant { Name = TenantName };
            dbContext.Tenants.Add(tenant);
            await dbContext.SaveChangesAsync();
        }

        var admin = await dbContext.Users.FirstOrDefaultAsync(item => item.QqId == AdminQqId);
        if (admin is null)
        {
            admin = new User
            {
                QqId = AdminQqId,
                DisplayName = AdminDisplayName,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.Users.Add(admin);
            await dbContext.SaveChangesAsync();
        }

        admin.DisplayName = AdminDisplayName;
        admin.PasswordHash = new PasswordHasher<User>().HashPassword(admin, AdminPassword);
        admin.UpdatedAt = now;

        var membership = await dbContext.UserTenants.FindAsync(tenant.Id, admin.Id);
        if (membership is null)
        {
            dbContext.UserTenants.Add(new UserTenant
            {
                TenantId = tenant.Id,
                UserId = admin.Id,
                Role = "super_admin",
                Status = "active",
                CreatedAt = now,
                UpdatedAt = now
            });
        }
        else
        {
            membership.Role = "super_admin";
            membership.Status = "active";
            membership.DeletedAt = null;
            membership.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
    }

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
