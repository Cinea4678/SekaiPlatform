using System.Text.Json;
using SekaiPlatform.SourceSync;

namespace SekaiPlatform.IntegrationTests;

public sealed class SourceSyncTests
{
    [Fact]
    public void ScenarioPathBuilder_BuildsPhase4StoryPaths()
    {
        Assert.Equal(
            "event_story/event_001/scenario/scenario_001.json",
            MoeSekaiScenarioClient.BuildRelativePath(new ScenarioDownload(
                SourceSyncConstants.EventStory,
                "scenario_001",
                "event_001",
                null)));
        Assert.Equal(
            "scenario/unitstory/unit_001/scenario_002.json",
            MoeSekaiScenarioClient.BuildRelativePath(new ScenarioDownload(
                SourceSyncConstants.MainStory,
                "scenario_002",
                "unit_001",
                null)));
        Assert.Equal(
            "character/member/card_001/scenario_003.json",
            MoeSekaiScenarioClient.BuildRelativePath(new ScenarioDownload(
                SourceSyncConstants.CardStory,
                "scenario_003",
                "card_001",
                null)));
        Assert.Equal(
            "scenario/actionset/group12/scenario_004.json",
            MoeSekaiScenarioClient.BuildRelativePath(new ScenarioDownload(
                SourceSyncConstants.AreaTalk,
                "scenario_004",
                null,
                12)));
        Assert.Equal(
            "scenario/special/special_001/scenario_005.json",
            MoeSekaiScenarioClient.BuildRelativePath(new ScenarioDownload(
                SourceSyncConstants.SpecialStory,
                "scenario_005",
                "special_001",
                null)));
    }

    [Fact]
    public void AreaTalkCategory_MatchesMoeSekaiRules()
    {
        Assert.Equal("event_145", GetCategory("""{"id":2373,"releaseConditionId":1,"scenarioId":"areatalk_mzk5","actionSetType":"normal","isNextGrade":false}"""));
        Assert.Equal("event_2", GetCategory("""{"id":1,"releaseConditionId":100123,"scenarioId":"areatalk_ev","actionSetType":"normal","isNextGrade":false}"""));
        Assert.Equal("aprilfool2022", GetCategory("""{"id":2,"releaseConditionId":1,"scenarioId":"areatalk_aprilfool2022_01","actionSetType":"limited","areaId":1}"""));
        Assert.Equal("limited_3", GetCategory("""{"id":3,"releaseConditionId":1,"scenarioId":"areatalk_limited","actionSetType":"limited","areaId":3}"""));
        Assert.Equal("grade1", GetCategory("""{"id":4,"releaseConditionId":1,"scenarioId":"areatalk_normal","actionSetType":"normal","isNextGrade":false}"""));
        Assert.Equal("grade2", GetCategory("""{"id":5,"releaseConditionId":1,"scenarioId":"areatalk_normal","actionSetType":"normal","isNextGrade":true}"""));
        Assert.Equal("theater", GetCategory("""{"id":6,"releaseConditionId":2000001,"scenarioId":"areatalk_theater","actionSetType":"normal","isNextGrade":false}"""));
    }

    [Fact]
    public void UnityScenarioParser_ExtractsDialogueSpecialEffectsAndSeparator()
    {
        using var document = JsonDocument.Parse("""
        {
          "ScenarioId": "scenario_test",
          "Snippets": [
            { "Index": 0, "Action": 1, "ReferenceIndex": 0 },
            { "Index": 1, "Action": 6, "ReferenceIndex": 0 },
            { "Index": 2, "Action": 6, "ReferenceIndex": 1 }
          ],
          "TalkData": [
            {
              "TalkCharacters": [{ "Character2dId": 10 }],
              "WindowDisplayName": "",
              "Body": "こんにちは",
              "Voices": [],
              "WhenFinishCloseWindow": 1
            }
          ],
          "SpecialEffectData": [
            { "EffectType": 18, "StringVal": "教室", "StringValSub": "", "IntVal": 0 },
            { "EffectType": 23, "StringVal": "選択肢", "StringValSub": "", "IntVal": 0 }
          ]
        }
        """);

        var lines = new UnityScenarioParser().Parse(
            document.RootElement,
            new Dictionary<int, Character2dInfo>
            {
                [10] = new("mob", 20, null)
            },
            new Dictionary<int, string>
            {
                [20] = "Mob Name"
            },
            new Dictionary<int, string>
            {
                [1] = "Game Character"
            });

        Assert.Collection(
            lines,
            line =>
            {
                Assert.Equal(1, line.LineNo);
                Assert.Equal("dialogue", line.LineType);
                Assert.Equal("Mob Name", line.Speaker);
                Assert.Equal("こんにちは", line.Text);
            },
            line => Assert.Equal("separator", line.LineType),
            line => Assert.Equal("upper_scene", line.LineType),
            line => Assert.Equal("choice", line.LineType));
    }

    private static string GetCategory(string json)
    {
        using var document = JsonDocument.Parse(json);
        return MoeSekaiCatalogBuilder.GetAreaTalkCategory(document.RootElement);
    }
}
