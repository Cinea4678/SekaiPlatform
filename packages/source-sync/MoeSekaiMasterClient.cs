using System.Text.Json;

namespace SekaiPlatform.SourceSync;

/// <summary>
/// Downloads Moe Sekai master JSON files used to build the source story catalog.
/// </summary>
/// <param name="httpClient">HTTP client configured for Moe Sekai requests.</param>
/// <param name="options">Moe Sekai source synchronization options.</param>
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

    /// <summary>
    /// Fetches the current version document and required master data files.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the request sequence.</param>
    /// <returns>The downloaded master data snapshot.</returns>
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

    /// <summary>
    /// Attempts to fetch a version document from the configured mirrors.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel mirror requests.</param>
    /// <returns>The cloned version JSON root, or <see langword="null"/> when all mirrors fail.</returns>
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

    /// <summary>
    /// Downloads one master file from the first reachable configured mirror.
    /// </summary>
    /// <param name="file">Master file name relative to each configured base URL.</param>
    /// <param name="cancellationToken">Token used to cancel mirror requests.</param>
    /// <returns>The cloned JSON array items from the master file.</returns>
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

    /// <summary>
    /// Reads a master JSON payload that may be either a raw array or a data wrapper.
    /// </summary>
    /// <param name="root">Root JSON element from the downloaded master file.</param>
    /// <returns>Cloned items from the resolved JSON array.</returns>
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

    /// <summary>
    /// Appends a relative master file path to a base URL.
    /// </summary>
    /// <param name="baseUrl">Configured master base URL.</param>
    /// <param name="path">Relative file path.</param>
    /// <returns>The combined absolute URL string.</returns>
    private static string CombineUrl(string baseUrl, string path)
    {
        return baseUrl.EndsWith("/", StringComparison.Ordinal)
            ? baseUrl + path
            : baseUrl + "/" + path;
    }
}

/// <summary>
/// Moe Sekai master data snapshot required by the catalog builders.
/// </summary>
/// <param name="Version">Optional upstream version document.</param>
/// <param name="Files">Downloaded master files keyed by file name.</param>
public sealed record MoeSekaiMasterData(
    JsonElement? Version,
    IReadOnlyDictionary<string, IReadOnlyList<JsonElement>> Files)
{
    /// <summary>
    /// Event master records.
    /// </summary>
    public IReadOnlyList<JsonElement> Events => Files["events.json"];

    /// <summary>
    /// Event story master records.
    /// </summary>
    public IReadOnlyList<JsonElement> EventStories => Files["eventStories.json"];

    /// <summary>
    /// Main unit story master records.
    /// </summary>
    public IReadOnlyList<JsonElement> UnitStories => Files["unitStories.json"];

    /// <summary>
    /// Card master records.
    /// </summary>
    public IReadOnlyList<JsonElement> Cards => Files["cards.json"];

    /// <summary>
    /// Card episode master records.
    /// </summary>
    public IReadOnlyList<JsonElement> CardEpisodes => Files["cardEpisodes.json"];

    /// <summary>
    /// Area talk action set master records.
    /// </summary>
    public IReadOnlyList<JsonElement> ActionSets => Files["actionSets.json"];

    /// <summary>
    /// Character 2D master records used to resolve speaker names.
    /// </summary>
    public IReadOnlyList<JsonElement> Character2ds => Files["character2ds.json"];

    /// <summary>
    /// Game character master records used to resolve speaker names.
    /// </summary>
    public IReadOnlyList<JsonElement> GameCharacters => Files["gameCharacters.json"];

    /// <summary>
    /// Mob character master records used to resolve speaker names.
    /// </summary>
    public IReadOnlyList<JsonElement> MobCharacters => Files["mobCharacters.json"];

    /// <summary>
    /// Unit profile master records used to name main story groups.
    /// </summary>
    public IReadOnlyList<JsonElement> UnitProfiles => Files["unitProfiles.json"];

    /// <summary>
    /// Special story master records.
    /// </summary>
    public IReadOnlyList<JsonElement> SpecialStories => Files["specialStories.json"];
}
