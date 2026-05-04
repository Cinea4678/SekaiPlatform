namespace SekaiPlatform.SourceSync;

public sealed record StorySyncDraft(
    StoryGroupDraft Group,
    StoryDraft Story,
    ScenarioDownload Download);

public sealed record StoryGroupDraft(
    string StoryType,
    string ExternalType,
    string ExternalId,
    int? DisplayNo,
    string Title,
    string? Subtitle,
    string Metadata);

public sealed record StoryDraft(
    string StoryType,
    string ScenarioId,
    string Title,
    int SortOrder,
    string Metadata);
