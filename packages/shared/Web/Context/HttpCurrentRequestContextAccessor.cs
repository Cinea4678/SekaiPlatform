using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SekaiPlatform.Shared.Web.Auth;
using SekaiPlatform.Shared.Web.Http;

namespace SekaiPlatform.Shared.Web.Context;

/// <summary>
/// Reads request context from the current HTTP request and validated token claims.
/// </summary>
/// <param name="httpContextAccessor">The ASP.NET Core HTTP context accessor.</param>
public sealed class HttpCurrentRequestContextAccessor(IHttpContextAccessor httpContextAccessor)
    : ICurrentRequestContextAccessor
{
    /// <inheritdoc />
    public CurrentRequestContext GetCurrent()
    {
        var httpContext = httpContextAccessor.HttpContext;
        var traceId = GetTraceId(httpContext);
        var userId = GetUserId(httpContext);
        var tenantId = GetTenantId(httpContext);

        return new CurrentRequestContext(traceId, userId, tenantId);
    }

    /// <summary>
    /// Resolves the request trace identifier from Activity, platform header, or HTTP context.
    /// </summary>
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

    /// <summary>
    /// Resolves the platform user identifier from validated token claims.
    /// </summary>
    private static long? GetUserId(HttpContext? httpContext)
    {
        return GetLongClaim(httpContext?.User, SekaiAuthDefaults.UserIdClaimType)
            ?? GetLongClaim(httpContext?.User, SekaiInternalAuthDefaults.SubjectUserIdClaimType)
            ?? GetLongClaim(httpContext?.User, ClaimTypes.NameIdentifier)
            ?? GetLongClaim(httpContext?.User, "sub");
    }

    /// <summary>
    /// Resolves the selected tenant identifier from validated token claims.
    /// </summary>
    private static long? GetTenantId(HttpContext? httpContext)
    {
        return GetLongClaim(httpContext?.User, SekaiAuthDefaults.TenantIdClaimType);
    }

    /// <summary>
    /// Reads and parses a numeric claim value.
    /// </summary>
    private static long? GetLongClaim(ClaimsPrincipal? user, string claimType)
    {
        var value = user?.FindFirstValue(claimType);
        return ParseLong(value);
    }

    /// <summary>
    /// Parses a platform identifier using invariant culture.
    /// </summary>
    private static long? ParseLong(string? value)
    {
        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }
}
