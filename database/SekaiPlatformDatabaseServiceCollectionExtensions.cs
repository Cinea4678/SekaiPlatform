using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SekaiPlatform.Database;

public static class SekaiPlatformDatabaseServiceCollectionExtensions
{
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
