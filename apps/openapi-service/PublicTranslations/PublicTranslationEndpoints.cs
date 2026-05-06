using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SekaiPlatform.Shared.Web.Auth;
using SekaiPlatform.Shared.Web.Context;

/// <summary>
/// Maps anonymous Open API endpoints for published translation reads.
/// </summary>
internal static class PublicTranslationEndpoints
{
    private const int MaxBatchSize = 100;

    /// <summary>
    /// Registers published translation Open API endpoints.
    /// </summary>
    public static IEndpointRouteBuilder MapPublicTranslationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/public/translations/{scenarioId}", async Task<IResult> (
            string scenarioId,
            IHttpClientFactory httpClientFactory,
            SekaiInternalTokenIssuer internalTokenIssuer,
            ICurrentRequestContextAccessor contextAccessor,
            CancellationToken cancellationToken) =>
        {
            var normalizedScenarioId = NormalizeScenarioId(scenarioId);
            if (normalizedScenarioId is null)
            {
                return OpenApiResults.Error(contextAccessor, StatusCodes.Status400BadRequest, "bad_request", "Bad request");
            }

            using var response = await SendAssetServiceAsync(
                httpClientFactory,
                internalTokenIssuer,
                HttpMethod.Get,
                $"/internal/public/translations/{Uri.EscapeDataString(normalizedScenarioId)}",
                content: null,
                cancellationToken);

            return await OpenApiResults.FromInternalResponseAsync(response, contextAccessor, cancellationToken);
        }).RequireRateLimiting("open-api");

        app.MapPost("/api/public/translations/batch", async Task<IResult> (
            HttpContext httpContext,
            IHttpClientFactory httpClientFactory,
            SekaiInternalTokenIssuer internalTokenIssuer,
            ICurrentRequestContextAccessor contextAccessor,
            CancellationToken cancellationToken) =>
        {
            var request = await ReadBatchRequestAsync(httpContext, cancellationToken);
            if (!TryNormalizeBatchRequest(request, out var normalizedRequest))
            {
                return OpenApiResults.Error(contextAccessor, StatusCodes.Status400BadRequest, "bad_request", "Bad request");
            }

            using var response = await SendAssetServiceAsync(
                httpClientFactory,
                internalTokenIssuer,
                HttpMethod.Post,
                "/internal/public/translations/batch",
                JsonContent.Create(normalizedRequest),
                cancellationToken);

            return await OpenApiResults.FromInternalResponseAsync(response, contextAccessor, cancellationToken);
        }).RequireRateLimiting("open-api");

        return app;
    }

    private static async Task<PublicTranslationBatchRequest?> ReadBatchRequestAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<PublicTranslationBatchRequest>(
                httpContext.Request.Body,
                cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task<HttpResponseMessage> SendAssetServiceAsync(
        IHttpClientFactory httpClientFactory,
        SekaiInternalTokenIssuer internalTokenIssuer,
        HttpMethod method,
        string path,
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, path)
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            internalTokenIssuer.Issue(
                SekaiInternalAuthDefaults.AssetServiceActor,
                SekaiInternalAuthDefaults.PublicTranslationReadScope));

        return await httpClientFactory
            .CreateClient("asset-service")
            .SendAsync(request, cancellationToken);
    }

    private static bool TryNormalizeBatchRequest(
        PublicTranslationBatchRequest? request,
        out PublicTranslationBatchRequest normalizedRequest)
    {
        normalizedRequest = new PublicTranslationBatchRequest([]);
        if (request?.ScenarioIds is not { Count: > 0 and <= MaxBatchSize })
        {
            return false;
        }

        var scenarioIds = new string[request.ScenarioIds.Count];
        for (var i = 0; i < request.ScenarioIds.Count; i++)
        {
            var scenarioId = NormalizeScenarioId(request.ScenarioIds[i]);
            if (scenarioId is null)
            {
                return false;
            }

            scenarioIds[i] = scenarioId;
        }

        normalizedRequest = new PublicTranslationBatchRequest(scenarioIds);
        return true;
    }

    private static string? NormalizeScenarioId(string? scenarioId)
    {
        var trimmed = scenarioId?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
