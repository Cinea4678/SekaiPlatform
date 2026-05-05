using System.Net.Http.Headers;
using SekaiPlatform.Shared.Web.Auth;
using SekaiPlatform.Shared.Web.Context;

/// <summary>
/// Maps API Service asset read endpoints that proxy frontend calls to Asset Service.
/// </summary>
internal static class AssetsProxyEndpoints
{
    /// <summary>
    /// Registers public story and translation read proxy endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapAssetsProxyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/story-types", async Task<IResult> (
            IHttpClientFactory httpClientFactory,
            SekaiInternalTokenIssuer internalTokenIssuer,
            ICurrentRequestContextAccessor contextAccessor,
            CancellationToken cancellationToken) =>
        {
            using var response = await SendAssetServiceAsync(
                httpClientFactory,
                internalTokenIssuer,
                contextAccessor.GetCurrent(),
                "/internal/story-types",
                cancellationToken);

            return await ForwardResponseAsync(response, cancellationToken);
        }).RequireAuthorization(SekaiAuthorizationPolicies.TenantSelected);

        app.MapGet("/api/story-groups", async Task<IResult> (
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
                "/internal/story-groups" + httpContext.Request.QueryString,
                cancellationToken);

            return await ForwardResponseAsync(response, cancellationToken);
        }).RequireAuthorization(SekaiAuthorizationPolicies.TenantSelected);

        app.MapGet("/api/story-groups/{storyGroupId:long}", async Task<IResult> (
            long storyGroupId,
            IHttpClientFactory httpClientFactory,
            SekaiInternalTokenIssuer internalTokenIssuer,
            ICurrentRequestContextAccessor contextAccessor,
            CancellationToken cancellationToken) =>
        {
            using var response = await SendAssetServiceAsync(
                httpClientFactory,
                internalTokenIssuer,
                contextAccessor.GetCurrent(),
                $"/internal/story-groups/{storyGroupId}",
                cancellationToken);

            return await ForwardResponseAsync(response, cancellationToken);
        }).RequireAuthorization(SekaiAuthorizationPolicies.TenantSelected);

        app.MapGet("/api/stories", async Task<IResult> (
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
                "/internal/stories" + httpContext.Request.QueryString,
                cancellationToken);

            return await ForwardResponseAsync(response, cancellationToken);
        }).RequireAuthorization(SekaiAuthorizationPolicies.TenantSelected);

        app.MapGet("/api/stories/{storyId:long}", async Task<IResult> (
            long storyId,
            IHttpClientFactory httpClientFactory,
            SekaiInternalTokenIssuer internalTokenIssuer,
            ICurrentRequestContextAccessor contextAccessor,
            CancellationToken cancellationToken) =>
        {
            using var response = await SendAssetServiceAsync(
                httpClientFactory,
                internalTokenIssuer,
                contextAccessor.GetCurrent(),
                $"/internal/stories/{storyId}",
                cancellationToken);

            return await ForwardResponseAsync(response, cancellationToken);
        }).RequireAuthorization(SekaiAuthorizationPolicies.TenantSelected);

        app.MapGet("/api/stories/{storyId:long}/source-lines", async Task<IResult> (
            long storyId,
            IHttpClientFactory httpClientFactory,
            SekaiInternalTokenIssuer internalTokenIssuer,
            ICurrentRequestContextAccessor contextAccessor,
            CancellationToken cancellationToken) =>
        {
            using var response = await SendAssetServiceAsync(
                httpClientFactory,
                internalTokenIssuer,
                contextAccessor.GetCurrent(),
                $"/internal/stories/{storyId}/source-lines",
                cancellationToken);

            return await ForwardResponseAsync(response, cancellationToken);
        }).RequireAuthorization(SekaiAuthorizationPolicies.TenantSelected);

        app.MapGet("/api/stories/{storyId:long}/translation-versions", async Task<IResult> (
            long storyId,
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
                $"/internal/stories/{storyId}/translation-versions" + httpContext.Request.QueryString,
                cancellationToken);

            return await ForwardResponseAsync(response, cancellationToken);
        }).RequireAuthorization(SekaiAuthorizationPolicies.TenantSelected);

        app.MapGet("/api/translation-versions/{translationVersionId:long}", async Task<IResult> (
            long translationVersionId,
            IHttpClientFactory httpClientFactory,
            SekaiInternalTokenIssuer internalTokenIssuer,
            ICurrentRequestContextAccessor contextAccessor,
            CancellationToken cancellationToken) =>
        {
            using var response = await SendAssetServiceAsync(
                httpClientFactory,
                internalTokenIssuer,
                contextAccessor.GetCurrent(),
                $"/internal/translation-versions/{translationVersionId}",
                cancellationToken);

            return await ForwardResponseAsync(response, cancellationToken);
        }).RequireAuthorization(SekaiAuthorizationPolicies.TenantSelected);

        app.MapGet("/api/translation-versions/{translationVersionId:long}/lines", async Task<IResult> (
            long translationVersionId,
            IHttpClientFactory httpClientFactory,
            SekaiInternalTokenIssuer internalTokenIssuer,
            ICurrentRequestContextAccessor contextAccessor,
            CancellationToken cancellationToken) =>
        {
            using var response = await SendAssetServiceAsync(
                httpClientFactory,
                internalTokenIssuer,
                contextAccessor.GetCurrent(),
                $"/internal/translation-versions/{translationVersionId}/lines",
                cancellationToken);

            return await ForwardResponseAsync(response, cancellationToken);
        }).RequireAuthorization(SekaiAuthorizationPolicies.TenantSelected);

        return app;
    }

    /// <summary>
    /// Sends an asset read request to Asset Service with the current user and tenant context.
    /// </summary>
    private static async Task<HttpResponseMessage> SendAssetServiceAsync(
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
                SekaiInternalAuthDefaults.AssetServiceActor,
                SekaiInternalAuthDefaults.AssetsReadScope,
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
