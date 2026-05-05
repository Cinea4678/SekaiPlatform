using System.Text.Json;

namespace SekaiPlatform.SourceSync.Catalog;

/// <summary>
/// Builds platform source story drafts from Moe Sekai master data.
/// </summary>
public sealed class MoeSekaiCatalogBuilder
{
    /// <summary>
    /// Builds de-duplicated story synchronization drafts for all supported source story types.
    /// </summary>
    /// <param name="masterData">Downloaded Moe Sekai master data snapshot.</param>
    /// <returns>Story sync drafts keyed by story type and scenario ID.</returns>
    public IReadOnlyList<StorySyncDraft> Build(MoeSekaiMasterData masterData)
    {
        var eventsById = masterData.Events
            .Where(item => item.GetIntOrNull("id") is not null)
            .ToDictionary(item => item.GetIntOrNull("id")!.Value);
        var cardsById = masterData.Cards
            .Where(item => item.GetIntOrNull("id") is not null)
            .ToDictionary(item => item.GetIntOrNull("id")!.Value);
        var unitProfilesByUnit = masterData.UnitProfiles
            .Select(item => new
            {
                Unit = item.GetStringOrNull("unit"),
                Name = item.GetStringOrNull("unitName")
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Unit))
            .ToDictionary(item => item.Unit!, item => item.Name, StringComparer.Ordinal);

        var drafts = new List<StorySyncDraft>();
        drafts.AddRange(EventStoryCatalogBuilder.Build(masterData, eventsById));
        drafts.AddRange(UnitStoryCatalogBuilder.Build(masterData, unitProfilesByUnit));
        drafts.AddRange(CardStoryCatalogBuilder.Build(masterData, cardsById));
        drafts.AddRange(AreaTalkCatalogBuilder.Build(masterData));
        drafts.AddRange(SpecialStoryCatalogBuilder.Build(masterData));

        return drafts
            .GroupBy(item => $"{item.Story.StoryType}:{item.Story.ScenarioId}", StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    /// <summary>
    /// Gets the platform grouping category for an area talk action set.
    /// </summary>
    /// <param name="actionSet">Moe Sekai action set master record.</param>
    /// <returns>The area talk category, or an empty string when unsupported.</returns>
    public static string GetAreaTalkCategory(JsonElement actionSet)
    {
        return AreaTalkCatalogBuilder.GetCategory(actionSet);
    }
}
