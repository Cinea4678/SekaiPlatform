namespace SekaiPlatform.SourceSync;

/// <summary>
/// Configuration for Moe Sekai source story synchronization.
/// </summary>
public sealed class MoeSekaiSourceSyncOptions
{
    /// <summary>
    /// Master data base URLs tried in order.
    /// </summary>
    public string[] MasterBaseUrls { get; set; } =
    [
        "https://sekaimaster.exmeaning.com/master/",
        "https://sk.exmeaning.com/master/"
    ];

    /// <summary>
    /// Version document URLs tried in order.
    /// </summary>
    public string[] VersionUrls { get; set; } =
    [
        "https://sekaimaster.exmeaning.com/versions/current_version.json",
        "https://sk.exmeaning.com/versions/current_version.json"
    ];

    /// <summary>
    /// Scenario asset base URLs tried in order.
    /// </summary>
    public string[] AssetBaseUrls { get; set; } =
    [
        "https://storage.exmeaning.com/sekai-jp-assets/",
        "https://storage2.exmeaning.com/sekai-jp-assets/",
        "https://storage.pjsk.moe/sekai-jp-assets/"
    ];

    /// <summary>
    /// Host allowlist for all configured Moe Sekai URLs.
    /// </summary>
    public string[] AllowedHosts { get; set; } =
    [
        "sekaimaster.exmeaning.com",
        "sk.exmeaning.com",
        "storage.exmeaning.com",
        "storage2.exmeaning.com",
        "storage.pjsk.moe"
    ];

    /// <summary>
    /// Allows HTTP URLs for local or explicitly trusted synchronization environments.
    /// </summary>
    public bool AllowInsecureHttp { get; set; }

    /// <summary>
    /// Per-request timeout used by Moe Sekai HTTP clients.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(3);

    /// <summary>
    /// Maximum number of failed scenario samples stored in sync job metadata.
    /// </summary>
    public int FailureSampleLimit { get; set; } = 20;

    /// <summary>
    /// Local wall-clock time used by the scheduled sync trigger.
    /// </summary>
    public string ScheduledLocalTime { get; set; } = "04:00";
}
