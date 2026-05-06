using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using SekaiPlatform.Shared.Web.Auth;
using SekaiPlatform.Shared.Web.Context;
using SekaiPlatform.Shared.Web.Hosting;
using SekaiPlatform.Shared.Web.Http;

const string UnknownOpenApiClientIpPartition = "unknown";

var builder = WebApplication.CreateBuilder(args);

builder.AddSekaiPlatformWebDefaults(
    SekaiAuthenticationMode.Anonymous,
    requireInternalTokenIssuer: true);
builder.Services.AddSekaiPlatformInternalHttpClient("asset-service", builder.Configuration, "AssetService");
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString();
        }

        var contextAccessor = context.HttpContext.RequestServices.GetRequiredService<ICurrentRequestContextAccessor>();
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new OpenApiErrorResponse("rate_limited", "Too many requests", contextAccessor.GetCurrent().TraceId),
            cancellationToken);
    };
    options.AddPolicy("open-api", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            GetOpenApiRateLimitPartitionKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
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

app.UseSekaiPlatformWebDefaults(
    useAuthentication: false,
    writeUnhandledExceptionAsync: OpenApiResults.WriteUnhandledExceptionAsync);
app.UseRateLimiter();
app.MapHealthChecks("/health");
app.MapPublicTranslationEndpoints();
app.MapFallback((ICurrentRequestContextAccessor contextAccessor) =>
    OpenApiResults.Error(contextAccessor, StatusCodes.Status404NotFound, "not_found", "Not found"))
    .RequireRateLimiting("open-api");

app.Run();

static string GetOpenApiRateLimitPartitionKey(HttpContext httpContext)
{
    if (TryGetForwardedForClientIp(httpContext, out var forwardedForIp))
    {
        return forwardedForIp;
    }

    var remoteIpAddress = httpContext.Connection.RemoteIpAddress;
    return remoteIpAddress is null
        ? UnknownOpenApiClientIpPartition
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
/// ASP.NET Core entry point marker for Open API Service hosting and tests.
/// </summary>
public partial class Program;
