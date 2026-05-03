using SekaiPlatform.Database;
using SekaiPlatform.Shared.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddSekaiPlatformWebDefaults();
builder.Services.AddSekaiPlatformDatabase(builder.Configuration);

var app = builder.Build();

app.UseSekaiPlatformWebDefaults();
app.MapHealthChecks("/health");

app.Run();
