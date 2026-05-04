using System.Text.Json;

namespace SekaiPlatform.SourceSync;

/// <summary>
/// Builds synchronization drafts for Moe Sekai event stories.
/// </summary>
internal static class EventStoryCatalogBuilder
{
    /// <summary>
    /// Enumerates event story drafts from event story and event master records.
    /// </summary>
    /// <param name="masterData">Downloaded Moe Sekai master data snapshot.</param>
    /// <param name="eventsById">Event master records keyed by event ID.</param>
    /// <returns>Event story synchronization drafts.</returns>
    public static IEnumerable<StorySyncDraft> Build(
        MoeSekaiMasterData masterData,
        IReadOnlyDictionary<int, JsonElement> eventsById)
    {
        foreach (var eventStory in masterData.EventStories)
        {
            var eventId = eventStory.GetIntOrNull("eventId");
            if (eventId is null)
            {
                continue;
            }

            eventsById.TryGetValue(eventId.Value, out var eventData);
            var assetbundleName = eventStory.GetStringOrNull("assetbundleName");
            var group = new StoryGroupDraft(
                SourceSyncConstants.EventStory,
                "sekai_event",
                eventId.Value.ToString(),
                eventId,
                eventData.ValueKind == JsonValueKind.Undefined
                    ? $"Event {eventId}"
                    : eventData.GetStringOrDefault("name", $"Event {eventId}"),
                eventStory.GetStringOrNull("outline"),
                SourceSyncJson.Serialize(new
                {
                    source = SourceSyncConstants.Source,
                    event_id = eventId,
                    assetbundle_name = assetbundleName
                }));

            foreach (var episode in eventStory.EnumerateArrayProperty("eventStoryEpisodes"))
            {
                var scenarioId = episode.GetStringOrNull("scenarioId");
                if (string.IsNullOrWhiteSpace(scenarioId))
                {
                    continue;
                }

                var episodeAssetbundleName = episode.GetStringOrNull("assetbundleName");
                yield return new StorySyncDraft(
                    group,
                    new StoryDraft(
                        SourceSyncConstants.EventStory,
                        scenarioId,
                        episode.GetStringOrDefault("title", $"Episode {episode.GetIntOrNull("episodeNo") ?? 0}"),
                        episode.GetIntOrNull("episodeNo") ?? 0,
                        SourceSyncJson.Serialize(new
                        {
                            source = SourceSyncConstants.Source,
                            event_id = eventId,
                            event_story_id = eventStory.GetIntOrNull("id"),
                            episode_no = episode.GetIntOrNull("episodeNo"),
                            episode_assetbundle_name = episodeAssetbundleName,
                            assetbundle_name = assetbundleName,
                            release_condition_id = episode.GetIntOrNull("releaseConditionId")
                        })),
                    new ScenarioDownload(
                        SourceSyncConstants.EventStory,
                        scenarioId,
                        assetbundleName ?? episodeAssetbundleName,
                        null));
            }
        }
    }
}
