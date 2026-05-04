using System.Text.Json;

namespace SekaiPlatform.SourceSync;

public sealed class MoeSekaiCatalogBuilder
{
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

    public static string GetAreaTalkCategory(JsonElement actionSet)
    {
        return AreaTalkCatalogBuilder.GetCategory(actionSet);
    }
}
