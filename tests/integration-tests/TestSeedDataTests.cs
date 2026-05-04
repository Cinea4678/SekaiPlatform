using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SekaiPlatform.Database;

namespace SekaiPlatform.IntegrationTests;

/// <summary>
/// Verifies seed data created by the shared integration database fixture.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class TestSeedDataTests(IntegrationTestDatabaseFixture fixture)
{
    /// <summary>
    /// Ensures the fixture creates the primary tenant and a usable super-admin account.
    /// </summary>
    [Fact]
    public async Task Fixture_SeedsDedicatedTenantAndSuperAdmin()
    {
        await using var dbContext = fixture.CreateDbContext();

        var seeded = await dbContext.UserTenants
            .Where(membership =>
                membership.Tenant!.Name == IntegrationTestDatabaseFixture.TenantName
                && membership.User!.QqId == IntegrationTestDatabaseFixture.AdminQqId)
            .Select(membership => new
            {
                membership.Role,
                membership.Status,
                membership.User!.PasswordHash
            })
            .SingleAsync();

        Assert.Equal(UserTenantRoles.SuperAdmin, seeded.Role);
        Assert.Equal(UserTenantStatuses.Active, seeded.Status);
        Assert.NotNull(seeded.PasswordHash);

        var verification = new PasswordHasher<User>().VerifyHashedPassword(
            new User { QqId = IntegrationTestDatabaseFixture.AdminQqId },
            seeded.PasswordHash,
            IntegrationTestDatabaseFixture.AdminPassword);
        Assert.NotEqual(PasswordVerificationResult.Failed, verification);
    }
}
