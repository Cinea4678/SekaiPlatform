using System.Text.Json;

namespace SekaiPlatform.SourceSync;

public sealed class MoeSekaiMasterClient(HttpClient httpClient, MoeSekaiSourceSyncOptions options)
{
    private static readonly string[] MasterFiles =
    [
        "events.json",
        "eventStories.json",
        "unitStories.json",
        "unitStoryEpisodeGroups.json",
        "cards.json",
        "cardEpisodes.json",
        "actionSets.json",
        "character2ds.json",
        "gameCharacters.json",
        "mobCharacters.json",
        "unitProfiles.json",
        "specialStories.json"
    ];

    public async Task<MoeSekaiMasterData> FetchAsync(CancellationToken cancellationToken)
    {
        var version = await FetchVersionAsync(cancellationToken);
        var files = new Dictionary<string, IReadOnlyList<JsonElement>>(StringComparer.Ordinal);
        foreach (var file in MasterFiles)
        {
            files[file] = await FetchMasterFileAsync(file, cancellationToken);
        }

        return new MoeSekaiMasterData(version, files);
    }

    private async Task<JsonElement?> FetchVersionAsync(CancellationToken cancellationToken)
    {
        foreach (var url in options.VersionUrls)
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
                return document.RootElement.Clone();
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
            }
        }

        return null;
    }

    private async Task<IReadOnlyList<JsonElement>> FetchMasterFileAsync(
        string file,
        CancellationToken cancellationToken)
    {
        foreach (var baseUrl in options.MasterBaseUrls)
        {
            var url = CombineUrl(baseUrl, file);
            try
            {
                using var response = await httpClient.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                return ReadArray(document.RootElement);
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

        throw new InvalidOperationException($"Failed to fetch Moe Sekai master file: {file}.");
    }

    private static IReadOnlyList<JsonElement> ReadArray(JsonElement root)
    {
        var array = root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Array
                ? data
                : root;

        if (array.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Master JSON root must be an array or an object with data array.");
        }

        return array.EnumerateArray().Select(item => item.Clone()).ToArray();
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        return baseUrl.EndsWith("/", StringComparison.Ordinal)
            ? baseUrl + path
            : baseUrl + "/" + path;
    }
}

public sealed record MoeSekaiMasterData(
    JsonElement? Version,
    IReadOnlyDictionary<string, IReadOnlyList<JsonElement>> Files)
{
    public IReadOnlyList<JsonElement> Events => Files["events.json"];
    public IReadOnlyList<JsonElement> EventStories => Files["eventStories.json"];
    public IReadOnlyList<JsonElement> UnitStories => Files["unitStories.json"];
    public IReadOnlyList<JsonElement> Cards => Files["cards.json"];
    public IReadOnlyList<JsonElement> CardEpisodes => Files["cardEpisodes.json"];
    public IReadOnlyList<JsonElement> ActionSets => Files["actionSets.json"];
    public IReadOnlyList<JsonElement> Character2ds => Files["character2ds.json"];
    public IReadOnlyList<JsonElement> GameCharacters => Files["gameCharacters.json"];
    public IReadOnlyList<JsonElement> MobCharacters => Files["mobCharacters.json"];
    public IReadOnlyList<JsonElement> UnitProfiles => Files["unitProfiles.json"];
    public IReadOnlyList<JsonElement> SpecialStories => Files["specialStories.json"];
}
