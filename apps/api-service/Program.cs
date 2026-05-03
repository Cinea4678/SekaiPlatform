using System.Text.Json.Serialization;
using SekaiPlatform.Database;
using SekaiPlatform.Shared.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddSekaiPlatformWebDefaults();
builder.Services.AddSekaiPlatformDatabase(builder.Configuration);
builder.Services.AddSekaiPlatformInternalHttpClient("auth-service", builder.Configuration, "AuthService");
builder.Services.AddSekaiPlatformInternalHttpClient("asset-service", builder.Configuration, "AssetService");
builder.Services.AddSekaiPlatformInternalHttpClient("search-service", builder.Configuration, "SearchService");

var app = builder.Build();

app.UseSekaiPlatformWebDefaults();
app.MapHealthChecks("/health");
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

await DatabaseInitializer.InitializeAsync(app);

app.Run();

static async Task<InternalServiceHealthItem> CheckHealthAsync(
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
