using System.Text.Json;

namespace SekaiPlatform.SourceSync;

public sealed class MoeSekaiScenarioClient(HttpClient httpClient, MoeSekaiSourceSyncOptions options)
{
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

    private static string RequireAssetbundleName(ScenarioDownload download)
    {
        return MoeSekaiUrlSafety.RequirePathSegment(
            download.AssetbundleName,
            "assetbundleName",
            download.StoryType,
            download.ScenarioId);
    }

    private static string RequireScenarioId(ScenarioDownload download)
    {
        return MoeSekaiUrlSafety.RequirePathSegment(
            download.ScenarioId,
            "scenarioId",
            download.StoryType,
            download.ScenarioId);
    }

    private static int RequireGroupId(ScenarioDownload download)
    {
        return download.GroupId is null or < 0
            ? throw new InvalidOperationException(
                $"Missing groupId for scenario {download.StoryType}:{download.ScenarioId}.")
            : download.GroupId.Value;
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        return baseUrl.EndsWith("/", StringComparison.Ordinal)
            ? baseUrl + path
            : baseUrl + "/" + path;
    }
}

public sealed record DownloadedScenario(string Url, JsonElement Json);

public sealed record ScenarioDownload(
    string StoryType,
    string ScenarioId,
    string? AssetbundleName,
    int? GroupId);
