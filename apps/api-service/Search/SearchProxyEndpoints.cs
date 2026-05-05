using System.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using SekaiPlatform.Database;
using SekaiPlatform.Shared.Web.Auth;
using SekaiPlatform.Shared.Web.Context;
using SekaiPlatform.Shared.Web.Responses;

/// <summary>
/// Maps API Service search endpoints that proxy frontend calls to Search Service.
/// </summary>
internal static class SearchProxyEndpoints
{
    /// <summary>
    /// Registers the public tenant-scoped language asset search endpoint.
    /// </summary>
    public static IEndpointRouteBuilder MapSearchProxyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/search", async Task<IResult> (
            IHttpClientFactory httpClientFactory,
            SekaiInternalTokenIssuer internalTokenIssuer,
            ICurrentRequestContextAccessor contextAccessor,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            using var response = await SendSearchServiceAsync(
                httpClientFactory,
                internalTokenIssuer,
                contextAccessor.GetCurrent(),
                "/internal/search" + httpContext.Request.QueryString,
                cancellationToken);

            return await ForwardResponseAsync(response, cancellationToken);
        }).RequireAuthorization(SekaiAuthorizationPolicies.TenantSelected);

        app.MapPost("/api/search/index/rebuild", async Task<IResult> (
            SekaiPlatformDbContext dbContext,
            IHttpClientFactory httpClientFactory,
            SekaiInternalTokenIssuer internalTokenIssuer,
            ICurrentRequestContextAccessor contextAccessor,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (!await IsCurrentTenantSuperAdminAsync(dbContext, contextAccessor, cancellationToken))
            {
                return Error(contextAccessor, StatusCodes.Status403Forbidden, "无权访问。");
            }

            using var response = await SendSearchIndexRebuildAsync(
                httpClientFactory,
                internalTokenIssuer,
                httpContext,
                cancellationToken);

            return await ForwardResponseAsync(response, cancellationToken);
        }).RequireAuthorization(SekaiAuthorizationPolicies.TenantSelected);

        return app;
    }

    /// <summary>
    /// Sends a search request to Search Service with the current user and tenant context.
    /// </summary>
    private static async Task<HttpResponseMessage> SendSearchServiceAsync(
        IHttpClientFactory httpClientFactory,
        SekaiInternalTokenIssuer internalTokenIssuer,
        CurrentRequestContext requestContext,
        string path,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            internalTokenIssuer.Issue(
                SekaiInternalAuthDefaults.SearchServiceActor,
                SekaiInternalAuthDefaults.SearchQueryScope,
                requestContext.UserId,
                requestContext.TenantId));

        return await httpClientFactory
            .CreateClient("search-service")
            .SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Sends a search index rebuild request to Search Service with a maintenance internal token.
    /// </summary>
    private static async Task<HttpResponseMessage> SendSearchIndexRebuildAsync(
        IHttpClientFactory httpClientFactory,
        SekaiInternalTokenIssuer internalTokenIssuer,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/internal/search/index/rebuild");
        if (httpContext.Request.ContentLength is not 0)
        {
            request.Content = new StreamContent(httpContext.Request.Body);
            if (!string.IsNullOrWhiteSpace(httpContext.Request.ContentType)
                && MediaTypeHeaderValue.TryParse(httpContext.Request.ContentType, out var contentType))
            {
                request.Content.Headers.ContentType = contentType;
            }
        }

        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            internalTokenIssuer.Issue(
                SekaiInternalAuthDefaults.SearchServiceActor,
                SekaiInternalAuthDefaults.SearchIndexRebuildScope));

        return await httpClientFactory
            .CreateClient("search-service")
            .SendAsync(request, cancellationToken);
    }

    /// <summary>
    /// Checks whether the current request user is a super administrator of the selected tenant.
    /// </summary>
    private static async Task<bool> IsCurrentTenantSuperAdminAsync(
        SekaiPlatformDbContext dbContext,
        ICurrentRequestContextAccessor contextAccessor,
        CancellationToken cancellationToken)
    {
        var context = contextAccessor.GetCurrent();
        if (context.UserId is null || context.TenantId is null)
        {
            return false;
        }

        var role = await dbContext.UserTenants
            .Where(item =>
                item.TenantId == context.TenantId.Value
                && item.UserId == context.UserId.Value
                && item.Status == UserTenantStatuses.Active)
            .Select(item => item.Role)
            .SingleOrDefaultAsync(cancellationToken);

        return role == UserTenantRoles.SuperAdmin;
    }

    /// <summary>
    /// Forwards an internal service response body, content type, and status code.
    /// </summary>
    private static async Task<IResult> ForwardResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
        return Results.Content(body, contentType, statusCode: (int)response.StatusCode);
    }

    private static IResult Error(
        ICurrentRequestContextAccessor contextAccessor,
        int statusCode,
        string message)
    {
        return Results.Json(
            new ErrorResponse(message, contextAccessor.GetCurrent().TraceId),
            statusCode: statusCode);
    }
}
