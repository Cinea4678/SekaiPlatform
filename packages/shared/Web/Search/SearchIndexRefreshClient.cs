using System.Net.Http.Json;
using System.Text.Json.Serialization;
using SekaiPlatform.Shared.Web.Auth;

namespace SekaiPlatform.Shared.Web.Search;

/// <summary>
/// Calls Search Service to refresh index documents after story data changes.
/// </summary>
/// <param name="httpClient">HTTP client configured with the Search Service base address.</param>
/// <param name="internalTokenIssuer">Issuer used to authorize internal search maintenance calls.</param>
/// <param name="options">Refresh batching and timeout options.</param>
public sealed class SearchIndexRefreshClient(
    HttpClient httpClient,
    SekaiInternalTokenIssuer internalTokenIssuer,
    SearchIndexRefreshOptions options)
{
    private const string RebuildPath = "/internal/search/index/rebuild";
    private const string StoryRefreshScope = "all";
    private const string SourceRefreshScope = "source";
    private const string TranslationRefreshScope = "translation";
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

        return await SendRefreshBatchesAsync(
            distinctStoryIds,
            Math.Max(1, options.StoryBatchSize),
            ids => new StoryIndexRefreshRequest(StoryRefreshScope, ids),
            SekaiInternalAuthDefaults.SearchIndexRebuildScope,
            tenantId: null,
            cancellationToken);
    }

    /// <summary>
    /// Refreshes only source index documents for the specified stories.
    /// </summary>
    /// <param name="storyIds">Story identifiers whose source-line search priority metadata changed.</param>
    /// <param name="cancellationToken">Token used to cancel the refresh request.</param>
    /// <returns>The refresh request result, including error response details when available.</returns>
    public async Task<SearchIndexRefreshResult> RefreshSourceStoriesAsync(
        IReadOnlyCollection<long> storyIds,
        CancellationToken cancellationToken)
    {
        var distinctStoryIds = storyIds.Distinct().ToArray();
        if (distinctStoryIds.Length == 0)
        {
            return SearchIndexRefreshResult.Succeeded();
        }

        return await SendRefreshBatchesAsync(
            distinctStoryIds,
            Math.Max(1, options.StoryBatchSize),
            ids => new StoryIndexRefreshRequest(SourceRefreshScope, ids),
            SekaiInternalAuthDefaults.SearchIndexRebuildScope,
            tenantId: null,
            cancellationToken);
    }

    /// <summary>
    /// Refreshes translation index documents for tenant-owned translation versions.
    /// </summary>
    /// <param name="tenantId">Tenant that owns the translation versions.</param>
    /// <param name="translationVersionIds">Translation version identifiers to refresh.</param>
    /// <param name="cancellationToken">Token used to cancel the refresh request.</param>
    /// <returns>The refresh request result, including error response details when available.</returns>
    public async Task<SearchIndexRefreshResult> RefreshTranslationVersionsAsync(
        long tenantId,
        IReadOnlyCollection<long> translationVersionIds,
        CancellationToken cancellationToken)
    {
        var distinctVersionIds = translationVersionIds.Distinct().ToArray();
        if (distinctVersionIds.Length == 0)
        {
            return SearchIndexRefreshResult.Succeeded();
        }

        return await SendRefreshBatchesAsync(
            distinctVersionIds,
            Math.Max(1, options.TranslationVersionBatchSize),
            ids => new TranslationIndexRefreshRequest(TranslationRefreshScope, tenantId, ids),
            SekaiInternalAuthDefaults.SearchTranslationRefreshScope,
            tenantId,
            cancellationToken);
    }

    /// <summary>
    /// Sends an authorized Search Service rebuild request.
    /// </summary>
    private async Task<SearchIndexRefreshResult> SendRefreshBatchesAsync(
        long[] ids,
        int batchSize,
        Func<long[], object> createBody,
        string scope,
        long? tenantId,
        CancellationToken cancellationToken)
    {
        var batchIndex = 0;
        foreach (var batch in ids.Chunk(batchSize))
        {
            batchIndex++;
            var result = await SendRefreshAsync(createBody(batch), scope, tenantId, cancellationToken);
            if (!result.Success)
            {
                return SearchIndexRefreshResult.Failed(
                    result.StatusCode,
                    $"搜索索引刷新第 {batchIndex} 批失败，批量大小 {batch.Length}。{result.Body}");
            }
        }

        return SearchIndexRefreshResult.Succeeded();
    }

    /// <summary>
    /// Sends one authorized Search Service rebuild request.
    /// </summary>
    private async Task<SearchIndexRefreshResult> SendRefreshAsync(
        object body,
        string scope,
        long? tenantId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, RebuildPath)
        {
            Content = JsonContent.Create(body)
        };

        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            internalTokenIssuer.Issue(
                SekaiInternalAuthDefaults.SearchServiceActor,
                scope,
                tenantId: tenantId));

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return SearchIndexRefreshResult.Succeeded();
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            return SearchIndexRefreshResult.Failed((int)response.StatusCode, Truncate(responseBody));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SearchIndexRefreshResult.Failed(null, "搜索索引刷新超时。");
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

    private sealed record StoryIndexRefreshRequest(
        [property: JsonPropertyName("scope")] string Scope,
        [property: JsonPropertyName("story_ids")] long[] StoryIds);

    private sealed record TranslationIndexRefreshRequest(
        [property: JsonPropertyName("scope")] string Scope,
        [property: JsonPropertyName("tenant_id")] long TenantId,
        [property: JsonPropertyName("translation_version_ids")] long[] TranslationVersionIds);
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
