using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SekaiPlatform.Database;

/// <summary>
/// Creates the platform database context for EF Core design-time commands.
/// </summary>
public sealed class SekaiPlatformDbContextFactory : IDesignTimeDbContextFactory<SekaiPlatformDbContext>
{
    /// <summary>
    /// Creates a configured database context for migrations and other design-time tooling.
    /// </summary>
    /// <param name="args">Command-line arguments supplied by EF Core tooling.</param>
    /// <returns>A configured <see cref="SekaiPlatformDbContext"/> instance.</returns>
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

    /// <summary>
    /// Builds a PostgreSQL connection string from Docker-style environment variables.
    /// </summary>
    /// <returns>A PostgreSQL connection string for local EF Core tooling.</returns>
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
