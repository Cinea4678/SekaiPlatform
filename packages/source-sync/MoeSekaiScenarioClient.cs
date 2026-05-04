using System.Text.Json;

namespace SekaiPlatform.SourceSync;

/// <summary>
/// Downloads scenario JSON assets from Moe Sekai asset mirrors.
/// </summary>
/// <param name="httpClient">HTTP client configured for scenario asset requests.</param>
/// <param name="options">Moe Sekai source synchronization options.</param>
public sealed class MoeSekaiScenarioClient(HttpClient httpClient, MoeSekaiSourceSyncOptions options)
{
    /// <summary>
    /// Downloads a scenario asset from the first reachable configured mirror.
    /// </summary>
    /// <param name="download">Scenario download descriptor built from master data.</param>
    /// <param name="cancellationToken">Token used to cancel mirror requests.</param>
    /// <returns>The downloaded scenario JSON and URL that served it.</returns>
    public async Task<DownloadedScenario> DownloadAsync(
        ScenarioDownload download,
        CancellationToken cancellationToken)
    {
        var paths = options.AssetBaseUrls
            .Select(baseUrl => CombineUrl(baseUrl, BuildRelativePath(download)))
            .ToArray();

        foreach (var url in paths)
        {
            try
            {
                using var response = await httpClient.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                return new DownloadedScenario(url, document.RootElement.Clone());
            }
            catch (HttpRequestException)
            {
            }
            catch (JsonException)
            {
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
            }
        }

        throw new InvalidOperationException(
            $"Failed to download scenario {download.StoryType}:{download.ScenarioId}.");
    }

    /// <summary>
    /// Builds the Moe Sekai asset-relative path for a scenario descriptor.
    /// </summary>
    /// <param name="download">Scenario download descriptor.</param>
    /// <returns>The asset-relative scenario JSON path.</returns>
    public static string BuildRelativePath(ScenarioDownload download)
    {
        return download.StoryType switch
        {
            SourceSyncConstants.EventStory =>
                $"event_story/{RequireAssetbundleName(download)}/scenario/{RequireScenarioId(download)}.json",
            SourceSyncConstants.MainStory =>
                $"scenario/unitstory/{RequireAssetbundleName(download)}/{RequireScenarioId(download)}.json",
            SourceSyncConstants.CardStory =>
                $"character/member/{RequireAssetbundleName(download)}/{RequireScenarioId(download)}.json",
            SourceSyncConstants.AreaTalk =>
                $"scenario/actionset/group{RequireGroupId(download)}/{RequireScenarioId(download)}.json",
            SourceSyncConstants.SpecialStory =>
                $"scenario/special/{RequireAssetbundleName(download)}/{RequireScenarioId(download)}.json",
            _ => throw new InvalidOperationException($"Unsupported story type: {download.StoryType}.")
        };
    }

    /// <summary>
    /// Requires and escapes the assetbundle name used in a scenario path.
    /// </summary>
    /// <param name="download">Scenario download descriptor.</param>
    /// <returns>The escaped assetbundle path segment.</returns>
    private static string RequireAssetbundleName(ScenarioDownload download)
    {
        return MoeSekaiUrlSafety.RequirePathSegment(
            download.AssetbundleName,
            "assetbundleName",
            download.StoryType,
            download.ScenarioId);
    }

    /// <summary>
    /// Requires and escapes the scenario ID used in a scenario path.
    /// </summary>
    /// <param name="download">Scenario download descriptor.</param>
    /// <returns>The escaped scenario ID path segment.</returns>
    private static string RequireScenarioId(ScenarioDownload download)
    {
        return MoeSekaiUrlSafety.RequirePathSegment(
            download.ScenarioId,
            "scenarioId",
            download.StoryType,
            download.ScenarioId);
    }

    /// <summary>
    /// Requires the area talk group ID used in an area talk asset path.
    /// </summary>
    /// <param name="download">Scenario download descriptor.</param>
    /// <returns>The non-negative area talk group ID.</returns>
    private static int RequireGroupId(ScenarioDownload download)
    {
        return download.GroupId is null or < 0
            ? throw new InvalidOperationException(
                $"Missing groupId for scenario {download.StoryType}:{download.ScenarioId}.")
            : download.GroupId.Value;
    }

    /// <summary>
    /// Appends an asset-relative path to a base URL.
    /// </summary>
    /// <param name="baseUrl">Configured asset base URL.</param>
    /// <param name="path">Asset-relative path.</param>
    /// <returns>The combined absolute URL string.</returns>
    private static string CombineUrl(string baseUrl, string path)
    {
        return baseUrl.EndsWith("/", StringComparison.Ordinal)
            ? baseUrl + path
            : baseUrl + "/" + path;
    }
}

/// <summary>
/// Scenario JSON downloaded from a Moe Sekai asset mirror.
/// </summary>
/// <param name="Url">Absolute URL that served the scenario JSON.</param>
/// <param name="Json">Cloned scenario JSON root.</param>
public sealed record DownloadedScenario(string Url, JsonElement Json);

/// <summary>
/// Describes how to locate a scenario asset for a source story.
/// </summary>
/// <param name="StoryType">Platform story type constant.</param>
/// <param name="ScenarioId">Moe Sekai scenario ID.</param>
/// <param name="AssetbundleName">Optional assetbundle name required by most story types.</param>
/// <param name="GroupId">Optional area talk group ID.</param>
public sealed record ScenarioDownload(
    string StoryType,
    string ScenarioId,
    string? AssetbundleName,
    int? GroupId);
