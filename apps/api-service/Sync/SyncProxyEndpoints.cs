using System.Net.Http.Headers;
using SekaiPlatform.Shared.Web.Auth;
using SekaiPlatform.Shared.Web.Context;

/// <summary>
/// Maps API Service synchronization endpoints that proxy frontend calls to Asset Service.
/// </summary>
internal static class SyncProxyEndpoints
{
    /// <summary>
    /// Registers public sync job endpoints for starting and inspecting source sync jobs.
    /// </summary>
    public static IEndpointRouteBuilder MapSyncProxyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/sync/jobs", async Task<IResult> (
            IHttpClientFactory httpClientFactory,
            SekaiInternalTokenIssuer internalTokenIssuer,
            ICurrentRequestContextAccessor contextAccessor,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            using var response = await SendAssetServiceAsync(
                httpClientFactory,
                internalTokenIssuer,
                contextAccessor.GetCurrent(),
                httpContext,
                HttpMethod.Post,
                "/internal/sync/jobs",
                SekaiInternalAuthDefaults.SyncJobsWriteScope,
                cancellationToken);

            return await ForwardResponseAsync(response, cancellationToken);
        }).RequireAuthorization(SekaiAuthorizationPolicies.TenantSelected);

        app.MapGet("/api/sync/jobs", async Task<IResult> (
            IHttpClientFactory httpClientFactory,
            SekaiInternalTokenIssuer internalTokenIssuer,
            ICurrentRequestContextAccessor contextAccessor,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            using var response = await SendAssetServiceAsync(
                httpClientFactory,
                internalTokenIssuer,
                contextAccessor.GetCurrent(),
                httpContext,
                HttpMethod.Get,
                "/internal/sync/jobs" + httpContext.Request.QueryString,
                SekaiInternalAuthDefaults.SyncJobsReadScope,
                cancellationToken);

            return await ForwardResponseAsync(response, cancellationToken);
        }).RequireAuthorization(SekaiAuthorizationPolicies.TenantSelected);

        app.MapGet("/api/sync/jobs/{syncJobId:long}", async Task<IResult> (
            long syncJobId,
            IHttpClientFactory httpClientFactory,
            SekaiInternalTokenIssuer internalTokenIssuer,
            ICurrentRequestContextAccessor contextAccessor,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            using var response = await SendAssetServiceAsync(
                httpClientFactory,
                internalTokenIssuer,
                contextAccessor.GetCurrent(),
                httpContext,
                HttpMethod.Get,
                $"/internal/sync/jobs/{syncJobId}",
                SekaiInternalAuthDefaults.SyncJobsReadScope,
                cancellationToken);

            return await ForwardResponseAsync(response, cancellationToken);
        }).RequireAuthorization(SekaiAuthorizationPolicies.TenantSelected);

        return app;
    }

    /// <summary>
    /// Sends a sync request to Asset Service with a scoped internal token.
    /// </summary>
    private static async Task<HttpResponseMessage> SendAssetServiceAsync(
        IHttpClientFactory httpClientFactory,
        SekaiInternalTokenIssuer internalTokenIssuer,
        CurrentRequestContext requestContext,
        HttpContext httpContext,
        HttpMethod method,
        string path,
        string scope,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, path);
        if (method == HttpMethod.Post && httpContext.Request.ContentLength is > 0)
        {
            request.Content = new StreamContent(httpContext.Request.Body);
            if (!string.IsNullOrWhiteSpace(httpContext.Request.ContentType))
            {
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(httpContext.Request.ContentType);
            }
        }

        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            internalTokenIssuer.Issue(
                SekaiInternalAuthDefaults.AssetServiceActor,
                scope,
                requestContext.UserId,
                requestContext.TenantId));

        return await httpClientFactory
            .CreateClient("asset-service")
            .SendAsync(request, cancellationToken);
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
}
