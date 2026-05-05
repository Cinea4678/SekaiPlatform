namespace SekaiPlatform.SourceSync.Catalog;

/// <summary>
/// Complete draft describing one source story and how to download its scenario.
/// </summary>
/// <param name="Group">Draft story group that owns the story.</param>
/// <param name="Story">Draft story record.</param>
/// <param name="Download">Scenario download descriptor for the story.</param>
public sealed record StorySyncDraft(
    StoryGroupDraft Group,
    StoryDraft Story,
    ScenarioDownload Download);

/// <summary>
/// Draft story group produced from Moe Sekai master data.
/// </summary>
/// <param name="StoryType">Platform story type constant.</param>
/// <param name="ExternalType">External grouping namespace.</param>
/// <param name="ExternalId">External group identifier inside the namespace.</param>
/// <param name="DisplayNo">Optional display order from the source data.</param>
/// <param name="Title">Group title shown to users.</param>
/// <param name="Subtitle">Optional group subtitle or outline.</param>
/// <param name="Metadata">Serialized source metadata.</param>
public sealed record StoryGroupDraft(
    string StoryType,
    string ExternalType,
    string ExternalId,
    int? DisplayNo,
    string Title,
    string? Subtitle,
    string Metadata);

/// <summary>
/// Draft story produced from Moe Sekai master data before database upsert.
/// </summary>
/// <param name="StoryType">Platform story type constant.</param>
/// <param name="ScenarioId">External scenario ID.</param>
/// <param name="Title">Story title shown to users.</param>
/// <param name="SortOrder">Order within the owning group.</param>
/// <param name="Metadata">Serialized source metadata.</param>
public sealed record StoryDraft(
    string StoryType,
    string ScenarioId,
    string Title,
    int SortOrder,
    string Metadata);
