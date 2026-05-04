namespace SekaiPlatform.SourceSync;

public sealed class MoeSekaiSourceSyncOptions
{
    public string[] MasterBaseUrls { get; set; } =
    [
        "https://sekaimaster.exmeaning.com/master/",
        "https://sk.exmeaning.com/master/"
    ];

    public string[] VersionUrls { get; set; } =
    [
        "https://sekaimaster.exmeaning.com/versions/current_version.json",
        "https://sk.exmeaning.com/versions/current_version.json"
    ];

    public string[] AssetBaseUrls { get; set; } =
    [
        "https://storage.exmeaning.com/sekai-jp-assets/",
        "https://storage2.exmeaning.com/sekai-jp-assets/",
        "https://storage.pjsk.moe/sekai-jp-assets/"
    ];

    public string[] AllowedHosts { get; set; } =
    [
        "sekaimaster.exmeaning.com",
        "sk.exmeaning.com",
        "storage.exmeaning.com",
        "storage2.exmeaning.com",
        "storage.pjsk.moe"
    ];

    public bool AllowInsecureHttp { get; set; }

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public int FailureSampleLimit { get; set; } = 20;

    public string ScheduledLocalTime { get; set; } = "04:00";
}
