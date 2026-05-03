using System.Text.Json.Serialization;

internal static class InternalServicesHealthEndpoints
{
    public static IEndpointRouteBuilder MapInternalServicesHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/internal-services/health", async (
            IHttpClientFactory httpClientFactory,
            CancellationToken cancellationToken) =>
        {
            var checks = await Task.WhenAll(
                CheckHealthAsync(httpClientFactory, "auth-service", cancellationToken),
                CheckHealthAsync(httpClientFactory, "asset-service", cancellationToken),
                CheckHealthAsync(httpClientFactory, "search-service", cancellationToken));

            var isHealthy = checks.All(check => check.Healthy);
            var response = new InternalServicesHealthResponse(
                isHealthy ? "healthy" : "unhealthy",
                checks);

            return Results.Json(
                response,
                statusCode: isHealthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
        });

        return app;
    }

    private static async Task<InternalServiceHealthItem> CheckHealthAsync(
        IHttpClientFactory httpClientFactory,
        string serviceName,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClientFactory
                .CreateClient(serviceName)
                .GetAsync("/health", cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            return new InternalServiceHealthItem(
                serviceName,
                response.IsSuccessStatusCode,
                (int)response.StatusCode,
                response.IsSuccessStatusCode ? "healthy" : "unhealthy",
                body,
                null);
        }
        catch (Exception exception)
        {
            return new InternalServiceHealthItem(
                serviceName,
                false,
                null,
                "unhealthy",
                null,
                exception.Message);
        }
    }
}

internal sealed record InternalServicesHealthResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("services")] IReadOnlyCollection<InternalServiceHealthItem> Services);

internal sealed record InternalServiceHealthItem(
    [property: JsonPropertyName("service")] string Service,
    [property: JsonPropertyName("healthy")] bool Healthy,
    [property: JsonPropertyName("status_code")] int? StatusCode,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("body")] string? Body,
    [property: JsonPropertyName("error")] string? Error);
