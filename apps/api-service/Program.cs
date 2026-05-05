using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using SekaiPlatform.Database;
using SekaiPlatform.Shared.Web.Hosting;
using SekaiPlatform.Shared.Web.Http;

const string UnknownImportClientIpPartition = "unknown";

var builder = WebApplication.CreateBuilder(args);

builder.AddSekaiPlatformWebDefaults(requireInternalTokenIssuer: true);
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
    options.AddPolicy("import-write", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetImportWriteRateLimitPartitionKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));
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
app.MapAssetsProxyEndpoints();
app.MapSearchProxyEndpoints();
app.MapSyncProxyEndpoints();
app.MapImportProxyEndpoints();
app.MapInternalServicesHealthEndpoints();

await DatabaseInitializer.InitializeAsync(app);

app.Run();

static string GetImportWriteRateLimitPartitionKey(HttpContext httpContext)
{
    if (TryGetForwardedForClientIp(httpContext, out var forwardedForIp))
    {
        return forwardedForIp;
    }

    var remoteIpAddress = httpContext.Connection.RemoteIpAddress;
    return remoteIpAddress is null
        ? UnknownImportClientIpPartition
        : NormalizeIpAddress(remoteIpAddress);
}

static bool TryGetForwardedForClientIp(HttpContext httpContext, out string partitionKey)
{
    var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].ToString();
    if (string.IsNullOrWhiteSpace(forwardedFor))
    {
        partitionKey = "";
        return false;
    }

    foreach (var part in forwardedFor.Split(','))
    {
        var candidate = part.Trim();
        if (candidate.Length == 0)
        {
            continue;
        }

        if (IPAddress.TryParse(candidate, out var ipAddress))
        {
            partitionKey = NormalizeIpAddress(ipAddress);
            return true;
        }

        partitionKey = "";
        return false;
    }

    partitionKey = "";
    return false;
}

static string NormalizeIpAddress(IPAddress ipAddress)
{
    return ipAddress.IsIPv4MappedToIPv6
        ? ipAddress.MapToIPv4().ToString()
        : ipAddress.ToString();
}

/// <summary>
/// ASP.NET Core entry point marker for API Service hosting and tests.
/// </summary>
public partial class Program;
