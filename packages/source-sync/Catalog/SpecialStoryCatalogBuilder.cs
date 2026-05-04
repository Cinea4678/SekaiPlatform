namespace SekaiPlatform.SourceSync;

internal static class SpecialStoryCatalogBuilder
{
    public static IEnumerable<StorySyncDraft> Build(MoeSekaiMasterData masterData)
    {
        foreach (var specialStory in masterData.SpecialStories)
        {
            var specialStoryId = specialStory.GetIntOrNull("id");
            if (specialStoryId is null)
            {
                continue;
            }

            var group = new StoryGroupDraft(
                SourceSyncConstants.SpecialStory,
                "special",
                specialStoryId.Value.ToString(),
                specialStoryId,
                specialStory.GetStringOrNull("title") ?? specialStory.GetStringOrNull("name") ?? $"Special {specialStoryId}",
                specialStory.GetStringOrNull("outline"),
                SourceSyncJson.Serialize(new
                {
                    source = SourceSyncConstants.Source,
                    special_story_id = specialStoryId
                }));

            foreach (var episode in specialStory.EnumerateArrayProperty("episodes"))
            {
                var scenarioId = episode.GetStringOrNull("scenarioId");
                if (string.IsNullOrWhiteSpace(scenarioId))
                {
                    continue;
                }

                var assetbundleName = episode.GetStringOrNull("assetbundleName")
                    ?? specialStory.GetStringOrNull("assetbundleName");
                yield return new StorySyncDraft(
                    group,
                    new StoryDraft(
                        SourceSyncConstants.SpecialStory,
                        scenarioId,
                        episode.GetStringOrDefault("title", $"Episode {episode.GetIntOrNull("episodeNo") ?? 0}"),
                        episode.GetIntOrNull("episodeNo") ?? 0,
                        SourceSyncJson.Serialize(new
                        {
                            source = SourceSyncConstants.Source,
                            special_story_id = specialStoryId,
                            episode_no = episode.GetIntOrNull("episodeNo"),
                            assetbundle_name = assetbundleName
                        })),
                    new ScenarioDownload(SourceSyncConstants.SpecialStory, scenarioId, assetbundleName, null));
            }
        }
    }
}
