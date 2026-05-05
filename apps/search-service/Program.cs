using SekaiPlatform.Database;
using SekaiPlatform.SearchService.Search;
using SekaiPlatform.Shared.Web.Auth;
using SekaiPlatform.Shared.Web.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddSekaiPlatformWebDefaults(SekaiAuthenticationMode.InternalToken);
builder.Services.AddSekaiPlatformDatabase(builder.Configuration);
builder.Services.Configure<SearchIndexOptions>(builder.Configuration.GetSection(SearchIndexOptions.SectionName));
builder.Services.AddHttpClient<ElasticsearchIndexClient>((services, client) =>
{
    var options = services
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<SearchIndexOptions>>()
        .Value;
    client.BaseAddress = new Uri(options.Url);
});
builder.Services.AddHttpClient<ElasticsearchSearchClient>((services, client) =>
{
    var options = services
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<SearchIndexOptions>>()
        .Value;
    client.BaseAddress = new Uri(options.Url);
});
builder.Services.AddScoped<SearchIndexRebuilder>();
builder.Services.AddHealthChecks()
    .AddCheck<ElasticsearchHealthCheck>("elasticsearch")
    .AddCheck<PostgresHealthCheck>("postgres");

var app = builder.Build();

app.UseSekaiPlatformWebDefaults();
app.MapHealthChecks("/health");
app.MapSearchIndexEndpoints();
app.MapSearchQueryEndpoints();

app.Run();

/// <summary>
/// ASP.NET Core entry point marker for Search Service hosting and tests.
/// </summary>
public partial class Program;
