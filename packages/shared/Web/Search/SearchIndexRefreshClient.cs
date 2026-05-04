using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace SekaiPlatform.Shared.Web;

/// <summary>
/// Calls Search Service to refresh index documents after story data changes.
/// </summary>
/// <param name="httpClient">HTTP client configured with the Search Service base address.</param>
/// <param name="options">Maintenance token options used to authorize the internal request.</param>
public sealed class SearchIndexRefreshClient(
    HttpClient httpClient,
    IOptions<SearchIndexMaintenanceOptions> options)
{
    private const string RebuildPath = "/internal/search/index/rebuild";
    private const string StoryRefreshScope = "all";
    private const int MaxLoggedErrorBodyLength = 512;

    /// <summary>
    /// Refreshes source and translation index documents for the specified stories.
    /// </summary>
    /// <param name="storyIds">Story identifiers whose indexed metadata may have changed.</param>
    /// <param name="cancellationToken">Token used to cancel the refresh request.</param>
    /// <returns>The refresh request result, including error response details when available.</returns>
    public async Task<SearchIndexRefreshResult> RefreshStoriesAsync(
        IReadOnlyCollection<long> storyIds,
        CancellationToken cancellationToken)
    {
        var distinctStoryIds = storyIds.Distinct().ToArray();
        if (distinctStoryIds.Length == 0)
        {
            return SearchIndexRefreshResult.Succeeded();
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, RebuildPath)
        {
            Content = JsonContent.Create(new SearchIndexRefreshRequest(StoryRefreshScope, distinctStoryIds))
        };

        var token = options.Value.MaintenanceToken;
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.TryAddWithoutValidation(SekaiHeaders.MaintenanceToken, token);
        }

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return SearchIndexRefreshResult.Succeeded();
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return SearchIndexRefreshResult.Failed((int)response.StatusCode, Truncate(body));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SearchIndexRefreshResult.Failed(null, "Search index refresh timed out.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return SearchIndexRefreshResult.Failed(null, exception.Message);
        }
    }

    /// <summary>
    /// Keeps logged maintenance error payloads bounded.
    /// </summary>
    private static string Truncate(string value)
    {
        return value.Length <= MaxLoggedErrorBodyLength ? value : value[..MaxLoggedErrorBodyLength];
    }

    /// <summary>
    /// Request body for refreshing all indexed documents tied to changed stories.
    /// </summary>
    /// <param name="Scope">Rebuild scope sent to Search Service.</param>
    /// <param name="StoryIds">Story identifiers to refresh.</param>
    private sealed record SearchIndexRefreshRequest(
        [property: JsonPropertyName("scope")] string Scope,
        [property: JsonPropertyName("story_ids")] long[] StoryIds);
}

/// <summary>
/// Result of requesting a Search Service index refresh.
/// </summary>
/// <param name="Success">Whether the refresh request completed successfully.</param>
/// <param name="StatusCode">HTTP status code returned by Search Service when available.</param>
/// <param name="Body">Bounded failure detail returned by Search Service or the HTTP client.</param>
public sealed record SearchIndexRefreshResult(
    bool Success,
    int? StatusCode,
    string? Body)
{
    /// <summary>
    /// Creates a successful refresh result.
    /// </summary>
    public static SearchIndexRefreshResult Succeeded() => new(true, null, null);

    /// <summary>
    /// Creates a failed refresh result.
    /// </summary>
    /// <param name="statusCode">HTTP status code returned by Search Service when available.</param>
    /// <param name="body">Bounded failure detail returned by Search Service or the HTTP client.</param>
    public static SearchIndexRefreshResult Failed(int? statusCode, string body) => new(false, statusCode, body);
}
