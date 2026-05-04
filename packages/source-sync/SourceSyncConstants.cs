namespace SekaiPlatform.SourceSync;

/// <summary>
/// Shared constants for source story synchronization.
/// </summary>
public static class SourceSyncConstants
{
    /// <summary>
    /// External source identifier stored in sync metadata.
    /// </summary>
    public const string Source = "moesekai";

    /// <summary>
    /// Sync job type stored in the database.
    /// </summary>
    public const string JobType = "source_story_sync";

    /// <summary>
    /// Manual sync trigger type.
    /// </summary>
    public const string TriggerManual = "manual";

    /// <summary>
    /// Scheduled sync trigger type.
    /// </summary>
    public const string TriggerScheduled = "scheduled";

    /// <summary>
    /// Sync job status before execution starts.
    /// </summary>
    public const string StatusPending = "pending";

    /// <summary>
    /// Sync job status while execution is active.
    /// </summary>
    public const string StatusRunning = "running";

    /// <summary>
    /// Sync job status after successful execution.
    /// </summary>
    public const string StatusSucceeded = "succeeded";

    /// <summary>
    /// Sync job status after failed execution.
    /// </summary>
    public const string StatusFailed = "failed";

    /// <summary>
    /// Event story source story type.
    /// </summary>
    public const string EventStory = "event_story";

    /// <summary>
    /// Main unit story source story type.
    /// </summary>
    public const string MainStory = "main_story";

    /// <summary>
    /// Card episode story source story type.
    /// </summary>
    public const string CardStory = "card_story";

    /// <summary>
    /// Area talk source story type.
    /// </summary>
    public const string AreaTalk = "area_talk";

    /// <summary>
    /// Special story source story type.
    /// </summary>
    public const string SpecialStory = "special_story";
}
