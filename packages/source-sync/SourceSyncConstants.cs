namespace SekaiPlatform.SourceSync;

public static class SourceSyncConstants
{
    public const string Source = "moesekai";
    public const string JobType = "source_story_sync";

    public const string TriggerManual = "manual";
    public const string TriggerScheduled = "scheduled";

    public const string StatusPending = "pending";
    public const string StatusRunning = "running";
    public const string StatusSucceeded = "succeeded";
    public const string StatusFailed = "failed";

    public const string EventStory = "event_story";
    public const string MainStory = "main_story";
    public const string CardStory = "card_story";
    public const string AreaTalk = "area_talk";
    public const string SpecialStory = "special_story";
}
