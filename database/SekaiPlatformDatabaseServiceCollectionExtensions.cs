using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SekaiPlatform.Database;

/// <summary>
/// Registers database services for Sekai Platform applications.
/// </summary>
public static class SekaiPlatformDatabaseServiceCollectionExtensions
{
    /// <summary>
    /// Adds the platform EF Core database context using the configured PostgreSQL connection.
    /// </summary>
    /// <param name="services">The service collection to register database services into.</param>
    /// <param name="configuration">The application configuration containing the Postgres connection string.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddSekaiPlatformDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

        services.AddDbContext<SekaiPlatformDbContext>(options => options.UseNpgsql(connectionString));

        return services;
    }
}
