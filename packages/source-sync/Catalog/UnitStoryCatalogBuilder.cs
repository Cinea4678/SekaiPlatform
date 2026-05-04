using System.Text.Json;

namespace SekaiPlatform.SourceSync;

internal static class UnitStoryCatalogBuilder
{
    public static IEnumerable<StorySyncDraft> Build(
        MoeSekaiMasterData masterData,
        IReadOnlyDictionary<string, string?> unitProfilesByUnit)
    {
        foreach (var unitStory in masterData.UnitStories)
        {
            var unitKey = unitStory.GetStringOrNull("unit")
                ?? unitStory.GetStringOrNull("unitStoryType")
                ?? unitStory.GetIntOrNull("seq")?.ToString()
                ?? unitStory.GetIntOrNull("id")?.ToString();
            if (string.IsNullOrWhiteSpace(unitKey))
            {
                continue;
            }

            var group = new StoryGroupDraft(
                SourceSyncConstants.MainStory,
                "sekai_unit",
                unitKey,
                unitStory.GetIntOrNull("seq") ?? unitStory.GetIntOrNull("id"),
                unitStory.GetStringOrNull("title")
                    ?? GetUnitProfileName(unitStory.GetStringOrNull("unit"), unitProfilesByUnit)
                    ?? unitStory.GetStringOrNull("unit")
                    ?? $"Unit {unitKey}",
                unitStory.GetStringOrNull("outline"),
                SourceSyncJson.Serialize(new
                {
                    source = SourceSyncConstants.Source,
                    unit = unitStory.GetStringOrNull("unit"),
                    unit_seq = unitStory.GetIntOrNull("seq")
                }));

            foreach (var story in EnumerateEpisodes(unitStory))
            {
                var episode = story.Episode;
                var scenarioId = episode.GetStringOrNull("scenarioId");
                if (string.IsNullOrWhiteSpace(scenarioId))
                {
                    continue;
                }

                yield return new StorySyncDraft(
                    group,
                    new StoryDraft(
                        SourceSyncConstants.MainStory,
                        scenarioId,
                        episode.GetStringOrDefault("title", $"Episode {story.SortOrder}"),
                        story.SortOrder,
                        SourceSyncJson.Serialize(new
                        {
                            source = SourceSyncConstants.Source,
                            unit = unitStory.GetStringOrNull("unit"),
                            unit_seq = unitStory.GetIntOrNull("seq"),
                            chapter_assetbundle_name = story.AssetbundleName,
                            unit_story_episode_group_id = episode.GetIntOrNull("unitStoryEpisodeGroupId"),
                            episode_no = episode.GetIntOrNull("episodeNo"),
                            release_condition_id = episode.GetIntOrNull("releaseConditionId")
                        })),
                    new ScenarioDownload(
                        SourceSyncConstants.MainStory,
                        scenarioId,
                        story.AssetbundleName,
                        null));
            }
        }
    }

    private static string? GetUnitProfileName(
        string? unit,
        IReadOnlyDictionary<string, string?> unitProfilesByUnit)
    {
        return unit is not null && unitProfilesByUnit.TryGetValue(unit, out var name)
            ? name
            : null;
    }

    private static IEnumerable<(JsonElement Episode, string? AssetbundleName, int SortOrder)> EnumerateEpisodes(
        JsonElement unitStory)
    {
        var order = 0;
        foreach (var chapter in unitStory.EnumerateArrayProperty("chapters"))
        {
            var assetbundleName = chapter.GetStringOrNull("assetbundleName");
            foreach (var episode in chapter.EnumerateArrayProperty("episodes"))
            {
                order++;
                yield return (episode, assetbundleName, episode.GetIntOrNull("episodeNo") ?? order);
            }
        }

        foreach (var episode in unitStory.EnumerateArrayProperty("episodes"))
        {
            order++;
            var assetbundleName = episode.GetStringOrNull("assetbundleName")
                ?? unitStory.GetStringOrNull("assetbundleName");
            yield return (episode, assetbundleName, episode.GetIntOrNull("episodeNo") ?? order);
        }
    }
}
