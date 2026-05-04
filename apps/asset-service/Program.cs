using SekaiPlatform.Database;
using SekaiPlatform.Shared.Web;
using SekaiPlatform.SourceSync;

var builder = WebApplication.CreateBuilder(args);

builder.AddSekaiPlatformWebDefaults();
builder.Services.AddSekaiPlatformDatabase(builder.Configuration);
builder.Services.AddSekaiPlatformInternalHttpClient("search-service", builder.Configuration, "SearchService");
builder.Services.AddMoeSekaiSourceSync(builder.Configuration);

var app = builder.Build();

app.UseSekaiPlatformWebDefaults();
app.MapHealthChecks("/health");
app.MapSyncEndpoints();

app.Run();

/// <summary>
/// ASP.NET Core entry point marker for Asset Service hosting and tests.
/// </summary>
public partial class Program;
