using System.Globalization;
using System.Text.Json;

namespace SekaiPlatform.SourceSync;

internal static class AreaTalkCatalogBuilder
{
    public static IEnumerable<StorySyncDraft> Build(MoeSekaiMasterData masterData)
    {
        foreach (var actionSet in masterData.ActionSets)
        {
            var id = actionSet.GetIntOrNull("id");
            var scenarioId = actionSet.GetStringOrNull("scenarioId");
            if (id is null || string.IsNullOrWhiteSpace(scenarioId))
            {
                continue;
            }

            var category = GetCategory(actionSet);
            if (string.IsNullOrWhiteSpace(category))
            {
                continue;
            }

            var groupId = id.Value / 100;
            var group = new StoryGroupDraft(
                SourceSyncConstants.AreaTalk,
                "action_set_category",
                category,
                null,
                GetTitle(category),
                null,
                SourceSyncJson.Serialize(new
                {
                    source = SourceSyncConstants.Source,
                    category
                }));

            yield return new StorySyncDraft(
                group,
                new StoryDraft(
                    SourceSyncConstants.AreaTalk,
                    scenarioId,
                    actionSet.GetStringOrNull("title") ?? $"Action Set {id}",
                    id.Value,
                    SourceSyncJson.Serialize(new
                    {
                        source = SourceSyncConstants.Source,
                        action_set_id = id,
                        area_id = actionSet.GetIntOrNull("areaId"),
                        category,
                        action_set_type = actionSet.GetStringOrNull("actionSetType"),
                        release_condition_id = actionSet.GetIntOrNull("releaseConditionId"),
                        group_id = groupId
                    })),
                new ScenarioDownload(SourceSyncConstants.AreaTalk, scenarioId, null, groupId));
        }
    }

    public static string GetCategory(JsonElement actionSet)
    {
        var scenarioId = actionSet.GetStringOrNull("scenarioId");
        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            return "";
        }

        var releaseConditionId = actionSet.GetIntOrNull("releaseConditionId") ?? 0;
        var condition = releaseConditionId.ToString(CultureInfo.InvariantCulture);
        if (condition.Length == 6 && condition[0] == '1')
        {
            return $"event_{int.Parse(condition[1..4], CultureInfo.InvariantCulture) + 1}";
        }

        if (actionSet.GetIntOrNull("id") == 2373)
        {
            return "event_145";
        }

        if (scenarioId.Contains("aprilfool", StringComparison.Ordinal))
        {
            return scenarioId.Split('_').FirstOrDefault(part => part.StartsWith("aprilfool", StringComparison.Ordinal))
                ?? "aprilfool";
        }

        if (actionSet.GetStringOrNull("actionSetType") == "limited")
        {
            return $"limited_{actionSet.GetIntOrNull("areaId") ?? 0}";
        }

        if (actionSet.GetStringOrNull("actionSetType") == "normal" && releaseConditionId == 1)
        {
            return actionSet.GetBoolOrNull("isNextGrade") == true ? "grade2" : "grade1";
        }

        return releaseConditionId is >= 2000000 and <= 2000036 ? "theater" : "";
    }

    private static string GetTitle(string category)
    {
        return category switch
        {
            "grade1" => "日常对话（第一学年）",
            "grade2" => "日常对话（第二学年）",
            "theater" => "剧场版",
            _ when category.StartsWith("event_", StringComparison.Ordinal) => $"活动区域对话 {category[6..]}",
            _ when category.StartsWith("limited_", StringComparison.Ordinal) => $"限定区域对话 {category[8..]}",
            _ when category.StartsWith("aprilfool", StringComparison.Ordinal) => $"愚人节区域对话 {category}",
            _ => category
        };
    }
}
