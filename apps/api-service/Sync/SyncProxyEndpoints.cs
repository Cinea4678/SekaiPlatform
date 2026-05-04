using System.Net.Http.Headers;
using SekaiPlatform.Shared.Web;

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
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            using var response = await SendAssetServiceAsync(
                httpClientFactory,
                httpContext,
                HttpMethod.Post,
                "/internal/sync/jobs",
                cancellationToken);

            return await ForwardResponseAsync(response, cancellationToken);
        }).RequireAuthorization(SekaiAuthorizationPolicies.TenantSelected);

        app.MapGet("/api/sync/jobs", async Task<IResult> (
            IHttpClientFactory httpClientFactory,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            using var response = await SendAssetServiceAsync(
                httpClientFactory,
                httpContext,
                HttpMethod.Get,
                "/internal/sync/jobs" + httpContext.Request.QueryString,
                cancellationToken);

            return await ForwardResponseAsync(response, cancellationToken);
        }).RequireAuthorization(SekaiAuthorizationPolicies.TenantSelected);

        app.MapGet("/api/sync/jobs/{syncJobId:long}", async Task<IResult> (
            long syncJobId,
            IHttpClientFactory httpClientFactory,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            using var response = await SendAssetServiceAsync(
                httpClientFactory,
                httpContext,
                HttpMethod.Get,
                $"/internal/sync/jobs/{syncJobId}",
                cancellationToken);

            return await ForwardResponseAsync(response, cancellationToken);
        }).RequireAuthorization(SekaiAuthorizationPolicies.TenantSelected);

        return app;
    }

    /// <summary>
    /// Sends a sync request to Asset Service while preserving request body and bearer authentication.
    /// </summary>
    private static async Task<HttpResponseMessage> SendAssetServiceAsync(
        IHttpClientFactory httpClientFactory,
        HttpContext httpContext,
        HttpMethod method,
        string path,
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

        if (httpContext.Request.Headers.TryGetValue("Authorization", out var authorization)
            && AuthenticationHeaderValue.TryParse(authorization.ToString(), out var header))
        {
            request.Headers.Authorization = header;
        }
        else if (httpContext.Request.Cookies.TryGetValue(SekaiAuthDefaults.AuthenticationCookieName, out var token)
            && !string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

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
