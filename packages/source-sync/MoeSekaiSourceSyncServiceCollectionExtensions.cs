using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SekaiPlatform.SourceSync.Catalog;

namespace SekaiPlatform.SourceSync;

/// <summary>
/// Registers Moe Sekai source synchronization services.
/// </summary>
public static class MoeSekaiSourceSyncServiceCollectionExtensions
{
    /// <summary>
    /// Adds Moe Sekai source synchronization clients, catalog builders, parser, and runner.
    /// </summary>
    /// <param name="services">Service collection to register into.</param>
    /// <param name="configuration">Application configuration containing the MoeSekai section.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddMoeSekaiSourceSync(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection("MoeSekai")
            .Get<MoeSekaiSourceSyncOptions>() ?? new MoeSekaiSourceSyncOptions();
        MoeSekaiUrlSafety.ValidateOptions(options);

        services.AddSingleton(options);
        services.AddHttpClient<MoeSekaiMasterClient>(client => client.Timeout = options.RequestTimeout);
        services.AddHttpClient<MoeSekaiScenarioClient>(client => client.Timeout = options.RequestTimeout);
        services.AddSingleton<MoeSekaiCatalogBuilder>();
        services.AddSingleton<UnityScenarioParser>();
        services.AddSingleton<SourceStorySyncExecutionGate>();
        services.AddHostedService<SourceStorySyncStartupRecoveryService>();
        services.AddScoped<SourceStorySyncRunner>();

        return services;
    }
}
