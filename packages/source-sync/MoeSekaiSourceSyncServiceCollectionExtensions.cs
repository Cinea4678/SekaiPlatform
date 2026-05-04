using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SekaiPlatform.SourceSync;

public static class MoeSekaiSourceSyncServiceCollectionExtensions
{
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
        services.AddScoped<SourceStorySyncRunner>();

        return services;
    }
}
