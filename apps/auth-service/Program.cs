using SekaiPlatform.Database;
using SekaiPlatform.Shared.Web.Auth;
using SekaiPlatform.Shared.Web.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddSekaiPlatformWebDefaults(SekaiAuthenticationMode.InternalToken);
builder.Services.AddSekaiPlatformDatabase(builder.Configuration);
builder.Services.AddScoped<AuthApplicationService>();
builder.Services.AddScoped<AuthTokenIssuer>();

var app = builder.Build();

app.UseSekaiPlatformWebDefaults();
app.MapHealthChecks("/health");
app.MapAuthEndpoints();

app.Run();

/// <summary>
/// ASP.NET Core entry point marker for Auth Service hosting and tests.
/// </summary>
public partial class Program;
