using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace SekaiPlatform.Shared.Web;

public sealed class HttpCurrentRequestContextAccessor(IHttpContextAccessor httpContextAccessor)
    : ICurrentRequestContextAccessor
{
    public CurrentRequestContext GetCurrent()
    {
        var httpContext = httpContextAccessor.HttpContext;
        var traceId = GetTraceId(httpContext);
        var userId = GetUserId(httpContext);
        var tenantId = GetTenantId(httpContext);

        return new CurrentRequestContext(traceId, userId, tenantId);
    }

    private static string GetTraceId(HttpContext? httpContext)
    {
        var activityTraceId = Activity.Current?.TraceId.ToString();
        if (!string.IsNullOrWhiteSpace(activityTraceId))
        {
            return activityTraceId;
        }

        if (httpContext?.Request.Headers.TryGetValue(SekaiHeaders.TraceId, out var headerTraceId) == true
            && !string.IsNullOrWhiteSpace(headerTraceId))
        {
            return headerTraceId.ToString();
        }

        return httpContext?.TraceIdentifier ?? Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
    }

    private static long? GetUserId(HttpContext? httpContext)
    {
        return GetLongClaim(httpContext?.User, SekaiAuthDefaults.UserIdClaimType)
            ?? GetLongClaim(httpContext?.User, ClaimTypes.NameIdentifier)
            ?? GetLongClaim(httpContext?.User, "sub")
            ?? GetLongHeader(httpContext, SekaiHeaders.UserId);
    }

    private static long? GetTenantId(HttpContext? httpContext)
    {
        return GetLongClaim(httpContext?.User, SekaiAuthDefaults.TenantIdClaimType)
            ?? GetLongHeader(httpContext, SekaiHeaders.TenantId);
    }

    private static long? GetLongClaim(ClaimsPrincipal? user, string claimType)
    {
        var value = user?.FindFirstValue(claimType);
        return ParseLong(value);
    }

    private static long? GetLongHeader(HttpContext? httpContext, string headerName)
    {
        if (httpContext?.Request.Headers.TryGetValue(headerName, out var value) != true)
        {
            return null;
        }

        return ParseLong(value.ToString());
    }

    private static long? ParseLong(string? value)
    {
        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}
