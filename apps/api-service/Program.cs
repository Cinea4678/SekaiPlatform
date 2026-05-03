using Microsoft.AspNetCore.RateLimiting;
using SekaiPlatform.Database;
using SekaiPlatform.Shared.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddSekaiPlatformWebDefaults();
builder.Services.AddSekaiPlatformDatabase(builder.Configuration);
builder.Services.AddSekaiPlatformInternalHttpClient("auth-service", builder.Configuration, "AuthService");
builder.Services.AddSekaiPlatformInternalHttpClient("asset-service", builder.Configuration, "AssetService");
builder.Services.AddSekaiPlatformInternalHttpClient("search-service", builder.Configuration, "SearchService");
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth-login", limiter =>
    {
        limiter.PermitLimit = 30;
        limiter.QueueLimit = 0;
        limiter.Window = TimeSpan.FromMinutes(1);
    });
});

var app = builder.Build();

app.Use(async (httpContext, next) =>
{
    httpContext.Request.Headers.Remove(SekaiHeaders.UserId);
    httpContext.Request.Headers.Remove(SekaiHeaders.TenantId);
    await next();
});

app.UseSekaiPlatformWebDefaults();
app.UseRateLimiter();
app.MapHealthChecks("/health");
app.MapAuthProxyEndpoints();
app.MapInternalServicesHealthEndpoints();

await DatabaseInitializer.InitializeAsync(app);

app.Run();

public partial class Program;
