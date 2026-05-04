using Microsoft.AspNetCore.RateLimiting;
using SekaiPlatform.Database;
using SekaiPlatform.Shared.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddSekaiPlatformWebDefaults();
builder.Services.AddSekaiPlatformDatabase(builder.Configuration);
builder.Services.AddScoped<AuthApplicationService>();
builder.Services.AddScoped<AuthTokenIssuer>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth-login", limiter =>
    {
        limiter.PermitLimit = 60;
        limiter.QueueLimit = 0;
        limiter.Window = TimeSpan.FromMinutes(1);
    });
});

var app = builder.Build();

app.UseSekaiPlatformWebDefaults();
app.UseRateLimiter();
app.MapHealthChecks("/health");
app.MapAuthEndpoints();

app.Run();

/// <summary>
/// ASP.NET Core entry point marker for Auth Service hosting and tests.
/// </summary>
public partial class Program;
