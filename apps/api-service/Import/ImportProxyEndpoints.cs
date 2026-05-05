using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using SekaiPlatform.Shared.Web.Auth;
using SekaiPlatform.Shared.Web.Context;

/// <summary>
/// Maps API Service import endpoints that proxy frontend calls to Asset Service.
/// </summary>
internal static class ImportProxyEndpoints
{
    /// <summary>
    /// Registers public translation import proxy endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapImportProxyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/import/translation-versions", async Task<IResult> (
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
                cancellationToken);

            return await ForwardResponseAsync(response, cancellationToken);
        })
        .RequireAuthorization(SekaiAuthorizationPolicies.TenantSelected)
        .RequireRateLimiting("import-write")
        .WithMetadata(new RequestSizeLimitAttribute(10 * 1024 * 1024));

        return app;
    }

    /// <summary>
    /// Sends a translation import request to Asset Service with a scoped internal token.
    /// </summary>
    private static async Task<HttpResponseMessage> SendAssetServiceAsync(
        IHttpClientFactory httpClientFactory,
        SekaiInternalTokenIssuer internalTokenIssuer,
        CurrentRequestContext requestContext,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/internal/import/translation-versions")
        {
            Content = new StreamContent(httpContext.Request.Body)
        };
        if (!string.IsNullOrWhiteSpace(httpContext.Request.ContentType)
            && MediaTypeHeaderValue.TryParse(httpContext.Request.ContentType, out var contentType))
        {
            request.Content.Headers.ContentType = contentType;
        }

        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            internalTokenIssuer.Issue(
                SekaiInternalAuthDefaults.AssetServiceActor,
                SekaiInternalAuthDefaults.TranslationsImportWriteScope,
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
