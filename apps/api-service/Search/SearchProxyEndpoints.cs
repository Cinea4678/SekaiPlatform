using System.Net.Http.Headers;
using SekaiPlatform.Shared.Web.Auth;
using SekaiPlatform.Shared.Web.Context;

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
