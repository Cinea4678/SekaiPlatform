using System.Text.Json;

namespace SekaiPlatform.SourceSync;

public sealed class UnityScenarioParser
{
    public IReadOnlyList<SourceLineDraft> Parse(
        JsonElement scenario,
        IReadOnlyDictionary<int, Character2dInfo> character2ds,
        IReadOnlyDictionary<int, string> mobCharacters,
        IReadOnlyDictionary<int, string> gameCharacters)
    {
        var lines = new List<SourceLineDraft>();
        var snippets = scenario.EnumerateArrayProperty("Snippets").ToArray();
        var talkData = scenario.EnumerateArrayProperty("TalkData").ToArray();
        var specialEffectData = scenario.EnumerateArrayProperty("SpecialEffectData").ToArray();

        foreach (var snippet in snippets)
        {
            var action = snippet.GetIntOrNull("Action");
            var referenceIndex = snippet.GetIntOrNull("ReferenceIndex");
            if (referenceIndex is null)
            {
                continue;
            }

            if (action == 1 && referenceIndex.Value >= 0 && referenceIndex.Value < talkData.Length)
            {
                AddTalkLine(lines, snippet, talkData[referenceIndex.Value], character2ds, mobCharacters, gameCharacters);
            }
            else if (action == 6 && referenceIndex.Value >= 0 && referenceIndex.Value < specialEffectData.Length)
            {
                AddSpecialEffectLine(lines, snippet, specialEffectData[referenceIndex.Value]);
            }
        }

        while (lines.Count > 0 && lines[^1].LineType == "separator")
        {
            lines.RemoveAt(lines.Count - 1);
        }

        return lines.Select((line, index) => line with { LineNo = index + 1 }).ToArray();
    }

    public static IReadOnlyDictionary<int, Character2dInfo> BuildCharacter2dMap(IEnumerable<JsonElement> character2ds)
    {
        return character2ds
            .Where(item => item.GetIntOrNull("id") is not null)
            .ToDictionary(
                item => item.GetIntOrNull("id")!.Value,
                item => new Character2dInfo(
                    item.GetStringOrNull("characterType") ?? "",
                    item.GetIntOrNull("characterId") ?? 0,
                    item.GetStringOrNull("unit")));
    }

    public static IReadOnlyDictionary<int, string> BuildMobCharacterMap(IEnumerable<JsonElement> mobCharacters)
    {
        return mobCharacters
            .Where(item => item.GetIntOrNull("id") is not null)
            .ToDictionary(
                item => item.GetIntOrNull("id")!.Value,
                item => item.GetStringOrNull("name") ?? $"Mob {item.GetIntOrNull("id")}");
    }

    public static IReadOnlyDictionary<int, string> BuildGameCharacterMap(IEnumerable<JsonElement> gameCharacters)
    {
        return gameCharacters
            .Where(item => item.GetIntOrNull("id") is not null)
            .ToDictionary(
                item => item.GetIntOrNull("id")!.Value,
                item => JoinName(
                    item.GetStringOrNull("firstName"),
                    item.GetStringOrNull("givenName"))
                    ?? $"GameCharacter {item.GetIntOrNull("id")}");
    }

    private static void AddTalkLine(
        List<SourceLineDraft> lines,
        JsonElement snippet,
        JsonElement talk,
        IReadOnlyDictionary<int, Character2dInfo> character2ds,
        IReadOnlyDictionary<int, string> mobCharacters,
        IReadOnlyDictionary<int, string> gameCharacters)
    {
        var text = talk.GetStringOrNull("Body");
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var character2dId = talk
            .EnumerateArrayProperty("TalkCharacters")
            .FirstOrDefault()
            .GetIntOrNull("Character2dId");
        var speaker = talk.GetStringOrNull("WindowDisplayName");
        if (string.IsNullOrWhiteSpace(speaker) && character2dId is not null)
        {
            speaker = ResolveCharacterName(character2dId.Value, character2ds, mobCharacters, gameCharacters);
        }

        lines.Add(new SourceLineDraft(
            0,
            "dialogue",
            speaker,
            text,
            SourceSyncJson.Serialize(new
            {
                snippet_index = snippet.GetIntOrNull("Index"),
                reference_index = snippet.GetIntOrNull("ReferenceIndex"),
                character2d_id = character2dId,
                voices = talk.TryGetProperty("Voices", out var voices) ? voices.Clone() : default(JsonElement?)
            })));

        if ((talk.GetIntOrNull("WhenFinishCloseWindow") ?? 0) != 0)
        {
            lines.Add(new SourceLineDraft(
                0,
                "separator",
                null,
                "",
                SourceSyncJson.Serialize(new
                {
                    snippet_index = snippet.GetIntOrNull("Index"),
                    reason = "when_finish_close_window"
                })));
        }
    }

    private static void AddSpecialEffectLine(
        List<SourceLineDraft> lines,
        JsonElement snippet,
        JsonElement specialEffect)
    {
        var effectType = specialEffect.GetIntOrNull("EffectType") ?? 0;
        var lineType = effectType switch
        {
            8 or 24 or 38 => "scene",
            18 => "upper_scene",
            23 => "choice",
            _ => null
        };
        if (lineType is null)
        {
            return;
        }

        var text = specialEffect.GetStringOrNull("StringVal");
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        lines.Add(new SourceLineDraft(
            0,
            lineType,
            null,
            text,
            SourceSyncJson.Serialize(new
            {
                snippet_index = snippet.GetIntOrNull("Index"),
                reference_index = snippet.GetIntOrNull("ReferenceIndex"),
                effect_type = effectType,
                string_val_sub = specialEffect.GetStringOrNull("StringValSub"),
                int_val = specialEffect.GetIntOrNull("IntVal")
            })));
    }

    private static string ResolveCharacterName(
        int character2dId,
        IReadOnlyDictionary<int, Character2dInfo> character2ds,
        IReadOnlyDictionary<int, string> mobCharacters,
        IReadOnlyDictionary<int, string> gameCharacters)
    {
        if (!character2ds.TryGetValue(character2dId, out var character))
        {
            return $"Character2D {character2dId}";
        }

        if (character.CharacterType == "mob" && mobCharacters.TryGetValue(character.CharacterId, out var mobName))
        {
            return mobName;
        }

        if (character.CharacterType == "game_character" && gameCharacters.TryGetValue(character.CharacterId, out var gameCharacterName))
        {
            return gameCharacterName;
        }

        return character.CharacterType == "game_character"
            ? $"GameCharacter {character.CharacterId}"
            : $"Character2D {character2dId}";
    }

    private static string? JoinName(string? firstName, string? givenName)
    {
        var parts = new[] { firstName, givenName }
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
        return parts.Length == 0 ? null : string.Concat(parts);
    }
}

public sealed record Character2dInfo(string CharacterType, int CharacterId, string? Unit);

public sealed record SourceLineDraft(
    int LineNo,
    string LineType,
    string? Speaker,
    string Text,
    string Metadata);
