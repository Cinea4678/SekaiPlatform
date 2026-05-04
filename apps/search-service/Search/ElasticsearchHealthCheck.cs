using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace SekaiPlatform.SearchService.Search;

/// <summary>
/// Verifies Elasticsearch is reachable for Search Service readiness checks.
/// </summary>
internal sealed class ElasticsearchHealthCheck(
    IHttpClientFactory httpClientFactory,
    IOptions<SearchIndexOptions> options) : IHealthCheck
{
    /// <summary>
    /// Checks the configured Elasticsearch root endpoint.
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient(nameof(ElasticsearchHealthCheck));
            client.BaseAddress = new Uri(options.Value.Url);
            using var response = await client.GetAsync("/", cancellationToken);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"Elasticsearch returned {(int)response.StatusCode}.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Elasticsearch is unreachable.", exception);
        }
    }
}
