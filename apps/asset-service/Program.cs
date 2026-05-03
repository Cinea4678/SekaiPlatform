using SekaiPlatform.Database;
using SekaiPlatform.Shared.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddSekaiPlatformWebDefaults();
builder.Services.AddSekaiPlatformDatabase(builder.Configuration);
builder.Services.AddSekaiPlatformInternalHttpClient("search-service", builder.Configuration, "SearchService");

var app = builder.Build();

app.UseSekaiPlatformWebDefaults();
app.MapHealthChecks("/health");

app.Run();
