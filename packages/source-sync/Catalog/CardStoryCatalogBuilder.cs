using System.Text.Json;

namespace SekaiPlatform.SourceSync;

/// <summary>
/// Builds synchronization drafts for Moe Sekai card stories.
/// </summary>
internal static class CardStoryCatalogBuilder
{
    /// <summary>
    /// Enumerates card story drafts from card episode and card master records.
    /// </summary>
    /// <param name="masterData">Downloaded Moe Sekai master data snapshot.</param>
    /// <param name="cardsById">Card master records keyed by card ID.</param>
    /// <returns>Card story synchronization drafts.</returns>
    public static IEnumerable<StorySyncDraft> Build(
        MoeSekaiMasterData masterData,
        IReadOnlyDictionary<int, JsonElement> cardsById)
    {
        foreach (var episode in masterData.CardEpisodes)
        {
            var cardId = episode.GetIntOrNull("cardId");
            var scenarioId = episode.GetStringOrNull("scenarioId");
            if (cardId is null || string.IsNullOrWhiteSpace(scenarioId) || !cardsById.TryGetValue(cardId.Value, out var card))
            {
                continue;
            }

            var partType = episode.GetStringOrNull("cardEpisodePartType") ?? "";
            var sortOrder = partType.EndsWith("2", StringComparison.Ordinal) ? 2 : 1;
            var assetbundleName = card.GetStringOrNull("assetbundleName");
            var group = new StoryGroupDraft(
                SourceSyncConstants.CardStory,
                "sekai_card",
                cardId.Value.ToString(),
                cardId,
                card.GetStringOrNull("prefix") ?? card.GetStringOrNull("title") ?? $"Card {cardId}",
                null,
                SourceSyncJson.Serialize(new
                {
                    source = SourceSyncConstants.Source,
                    card_id = cardId,
                    assetbundle_name = assetbundleName,
                    character_id = card.GetIntOrNull("characterId")
                }));

            yield return new StorySyncDraft(
                group,
                new StoryDraft(
                    SourceSyncConstants.CardStory,
                    scenarioId,
                    episode.GetStringOrNull("title") ?? (sortOrder == 2 ? "後編" : "前編"),
                    sortOrder,
                    SourceSyncJson.Serialize(new
                    {
                        source = SourceSyncConstants.Source,
                        card_id = cardId,
                        card_episode_part_type = partType,
                        assetbundle_name = assetbundleName
                    })),
                new ScenarioDownload(SourceSyncConstants.CardStory, scenarioId, assetbundleName, null));
        }
    }
}
