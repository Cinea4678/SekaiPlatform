using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SekaiPlatform.Database;

public sealed class SekaiPlatformDbContextFactory : IDesignTimeDbContextFactory<SekaiPlatformDbContext>
{
    public SekaiPlatformDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
            ?? CreateConnectionStringFromPostgresEnvironment();

        var options = new DbContextOptionsBuilder<SekaiPlatformDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new SekaiPlatformDbContext(options);
    }

    private static string CreateConnectionStringFromPostgresEnvironment()
    {
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Set ConnectionStrings__Postgres, POSTGRES_CONNECTION_STRING, or POSTGRES_PASSWORD for EF Core commands.");
        }

        var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
        var database = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "sekai_platform";
        var username = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "sekai_platform";

        return $"Host={host};Port={port};Database={database};Username={username};Password={password}";
    }
}
